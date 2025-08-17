' Utilities/ThemeManager.vb - Theme management for SimpleIDE
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Reflection
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Managers
    
    Public Class ThemeManager
        
        ' Private fields
        Private pSettingsManager As SettingsManager
        Private pCurrentTheme As EditorTheme
        Private pAvailableThemes As Dictionary(Of String, EditorTheme)
        Private pCssProvider As CssProvider
        Private pCustomThemes As List(Of EditorTheme)
        
        ' Events
        Public Event ThemeChanged(vTheme As EditorTheme)
        Public Event ThemeApplied(vThemeName As String)
        Public Event ThemeListChanged()
        
        ' Constructor
        Public Sub New(vSettingsManager As SettingsManager)
            pSettingsManager = vSettingsManager
            pAvailableThemes = New Dictionary(Of String, EditorTheme)
            pCustomThemes = New List(Of EditorTheme)
            pCssProvider = New CssProvider()
            
            ' Initialize themes
            LoadBuiltInThemes()
            LoadCustomThemes()
            
            ' Load current theme from settings
            Dim lThemeName As String = pSettingsManager.GetSetting("CurrentTheme", "Default Dark")
            SetTheme(lThemeName)
        End Sub
        
        ' Get current theme name
        Public Function GetCurrentTheme() As String
            Return If(pCurrentTheme?.Name, "Default Dark")
        End Function
        
        ' Get current theme object
        Public Function GetCurrentThemeObject() As EditorTheme
            Return pCurrentTheme
        End Function
        
        ' Get list of available theme names
        Public Function GetAvailableThemes() As List(Of String)
            Dim lThemes As New List(Of String)
            
            for each lThemeName in pAvailableThemes.Keys
                lThemes.Add(lThemeName)
            Next
            
            Return lThemes
        End Function
        
        ' Get theme by name
        Public Function GetTheme(vThemeName As String) As EditorTheme
            If pAvailableThemes.ContainsKey(vThemeName) Then
                Return pAvailableThemes(vThemeName)
            End If
            
            Return Nothing
        End Function
        
        ' Set current theme
        Public Sub SetTheme(vThemeName As String)
            Try
                If Not pAvailableThemes.ContainsKey(vThemeName) Then
                    Console.WriteLine($"Theme '{vThemeName}' not found, using default")
                    vThemeName = "Default Dark"
                End If
                
                pCurrentTheme = pAvailableThemes(vThemeName)
                
                ' Save to settings
                pSettingsManager.SetSetting("CurrentTheme", vThemeName)
                
                ' Apply theme
                ApplyCurrentTheme()
                
                ' Raise events
                RaiseEvent ThemeChanged(pCurrentTheme)
                RaiseEvent ThemeApplied(vThemeName)
                
            Catch ex As Exception
                Console.WriteLine($"SetTheme error: {ex.Message}")
            End Try
        End Sub
        
        ' Apply theme by name
        Public Sub ApplyTheme(vThemeName As String)
            SetTheme(vThemeName)
        End Sub
        
        ' Apply current theme
        Public Sub ApplyCurrentTheme()
            Try
                If pCurrentTheme Is Nothing Then Return
                
                ' Generate CSS from theme
                Dim lCss As String = GenerateThemeCss(pCurrentTheme)
                
                ' Remove ALL previous CSS providers to ensure clean slate
                RemoveAllThemeProviders()
                
                ' Create new CSS provider
                pCssProvider = New CssProvider()
                pCssProvider.LoadFromData(lCss)
                
                ' Apply with high priority to override default GTK theme
                StyleContext.AddProviderForScreen(
                    Gdk.Screen.Default,
                    pCssProvider,
                    CUInt(StyleProviderPriority.User)  ' Use USER priority (800) for highest precedence
                )
                
                Console.WriteLine($"Applied theme: {pCurrentTheme.Name}")
                
                ' Force GTK to refresh all widgets
                ForceGlobalRefresh()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyCurrentTheme error: {ex.Message}")
            End Try
        End Sub

        
        ' Generate CSS from theme
        Private Function GenerateThemeCss(vTheme As EditorTheme) As String
            Try
                Dim lCss As New Text.StringBuilder()
                
                ' Global styles
                lCss.AppendLine("/* SimpleIDE Theme CSS */")
                lCss.AppendLine($"* {{")
                lCss.AppendLine($"    color: {vTheme.ForegroundColor};")
                lCss.AppendLine($"    background-color: {vTheme.BackgroundColor};")
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' Window styles
                lCss.AppendLine($"window {{")
                lCss.AppendLine($"    background-color: {vTheme.BackgroundColor};")
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' Editor styles - FIXED: Changed pt to px
                lCss.AppendLine($".Editor {{")
                lCss.AppendLine($"    font-family: {vTheme.FontFamily};")
                lCss.AppendLine($"    font-size: {vTheme.FontSize}px;")  ' Changed from pt to px
                lCss.AppendLine($"    color: {vTheme.ForegroundColor};")
                lCss.AppendLine($"    background-color: {vTheme.BackgroundColor};")
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' TreeView styles
                lCss.AppendLine($"treeview {{")
                If vTheme.IsDarkTheme Then
                    lCss.AppendLine($"    background-color: #252526;")
                    lCss.AppendLine($"    color: #CCCCCC;")
                Else
                    lCss.AppendLine($"    background-color: #F5F5F5;")
                    lCss.AppendLine($"    color: #000000;")
                End If
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                lCss.AppendLine($"treeview:selected {{")
                lCss.AppendLine($"    background-color: {vTheme.SelectionColor};")
                lCss.AppendLine($"    color: #FFFFFF;")
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' Notebook (tab control) styles
                lCss.AppendLine($"Notebook {{")
                If vTheme.IsDarkTheme Then
                    lCss.AppendLine($"    background-color: #2D2D30;")
                    lCss.AppendLine($"    border-color: #3E3E42;")
                Else
                    lCss.AppendLine($"    background-color: #F3F3F3;")
                    lCss.AppendLine($"    border-color: #CCCEDB;")
                End If
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' Button styles
                lCss.AppendLine($"button {{")
                If vTheme.IsDarkTheme Then
                    lCss.AppendLine($"    background-color: #3E3E42;")
                    lCss.AppendLine($"    border-color: #555555;")
                Else
                    lCss.AppendLine($"    background-color: #FDFDFE;")
                    lCss.AppendLine($"    border-color: #C8C8C8;")
                End If
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                lCss.AppendLine($"button:hover {{")
                If vTheme.IsDarkTheme Then
                    lCss.AppendLine($"    background-color: #4B4B4D;")
                Else
                    lCss.AppendLine($"    background-color: #F0F0F0;")
                End If
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' Menu styles
                lCss.AppendLine($"menu, menubar, menuitem {{")
                If vTheme.IsDarkTheme Then
                    lCss.AppendLine($"    background-color: #2D2D30;")
                    lCss.AppendLine($"    color: #CCCCCC;")
                Else
                    lCss.AppendLine($"    background-color: #F0F0F0;")
                    lCss.AppendLine($"    color: #000000;")
                End If
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                lCss.AppendLine($"menuitem:hover {{")
                lCss.AppendLine($"    background-color: {vTheme.SelectionColor};")
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' Statusbar styles
                lCss.AppendLine($"statusbar {{")
                If vTheme.IsDarkTheme Then
                    lCss.AppendLine($"    background-color: #007ACC;")
                    lCss.AppendLine($"    color: #FFFFFF;")
                Else
                    lCss.AppendLine($"    background-color: #007ACC;")
                    lCss.AppendLine($"    color: #FFFFFF;")
                End If
                lCss.AppendLine($"}}")
                lCss.AppendLine()
                
                ' Paned separator styles
                lCss.AppendLine($"paned > separator {{")
                If vTheme.IsDarkTheme Then
                    lCss.AppendLine($"    background-color: #3E3E42;")
                    lCss.AppendLine($"    background-image: none;")
                Else
                    lCss.AppendLine($"    background-color: #CCCEDB;")
                    lCss.AppendLine($"    background-image: none;")
                End If
                lCss.AppendLine($"}}")
                
                Return lCss.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"GenerateThemeCss error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' Load built-in themes
        Private Sub LoadBuiltInThemes()
            Try
                ' Load predefined themes from EditorTheme
                Dim lBuiltInThemes As List(Of EditorTheme) = GetAllBuiltInThemes()
                
                for each lTheme in lBuiltInThemes
                    pAvailableThemes(lTheme.Name) = lTheme
                Next
                
                Console.WriteLine($"loaded {pAvailableThemes.Count} built-in themes")
                
            Catch ex As Exception
                Console.WriteLine($"LoadBuiltInThemes error: {ex.Message}")
                
                ' Ensure at least one theme exists
                If pAvailableThemes.Count = 0 Then
                    Dim lDefaultTheme As New EditorTheme("Default Dark")
                    pAvailableThemes(lDefaultTheme.Name) = lDefaultTheme
                End If
            End Try
        End Sub
        
        ' Get all built-in themes including popular ones
        Private Function GetAllBuiltInThemes() As List(Of EditorTheme)
            Dim lThemes As New List(Of EditorTheme)
            
            ' Get base themes
            lThemes.AddRange(EditorTheme.GetBuiltInThemes())
            
            ' Add more popular themes
            
            ' Monokai theme
            Dim lMonokai As New EditorTheme("Monokai")
            lMonokai.Description = "Popular dark theme"
            lMonokai.IsDarkTheme = True
            lMonokai.BackgroundColor = "#272822"
            lMonokai.ForegroundColor = "#F8F8F2"
            lMonokai.SelectionColor = "#49483E"
            lMonokai.CurrentLineColor = "#3E3D32"
            lMonokai.LineNumberColor = "#90908A"
            lMonokai.LineNumberBackgroundColor = "#272822"
            lMonokai.CurrentLineNumberColor = "#F8F8F2"
            lMonokai.CursorColor = "#F8F8F0"
            lMonokai.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#F92672"
            lMonokai.SyntaxColors(SyntaxColorSet.Tags.eType) = "#66D9EF"
            lMonokai.SyntaxColors(SyntaxColorSet.Tags.eString) = "#E6DB74"
            lMonokai.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#75715E"
            lMonokai.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#AE81FF"
            lMonokai.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#F8F8F2"
            lMonokai.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#49483E"
            lThemes.Add(lMonokai)
            
            ' Solarized Dark theme
            Dim lSolarizedDark As New EditorTheme("Solarized Dark")
            lSolarizedDark.Description = "Precision colors for machines and people"
            lSolarizedDark.IsDarkTheme = True
            lSolarizedDark.BackgroundColor = "#002B36"
            lSolarizedDark.ForegroundColor = "#839496"
            lSolarizedDark.SelectionColor = "#073642"
            lSolarizedDark.CurrentLineColor = "#073642"
            lSolarizedDark.LineNumberColor = "#586E75"
            lSolarizedDark.LineNumberBackgroundColor = "#002B36"
            lSolarizedDark.CurrentLineNumberColor = "#93A1A1"
            lSolarizedDark.CursorColor = "#D33682"
            lSolarizedDark.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#859900"
            lSolarizedDark.SyntaxColors(SyntaxColorSet.Tags.eType) = "#268BD2"
            lSolarizedDark.SyntaxColors(SyntaxColorSet.Tags.eString) = "#2AA198"
            lSolarizedDark.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#586E75"
            lSolarizedDark.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#6C71C4"
            lSolarizedDark.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#839496"
            lSolarizedDark.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#073642"
            lThemes.Add(lSolarizedDark)
            
            ' Solarized Light theme
            Dim lSolarizedLight As New EditorTheme("Solarized Light")
            lSolarizedLight.Description = "Precision colors for machines and people"
            lSolarizedLight.IsDarkTheme = False
            lSolarizedLight.BackgroundColor = "#FDF6E3"
            lSolarizedLight.ForegroundColor = "#657B83"
            lSolarizedLight.SelectionColor = "#EEE8D5"
            lSolarizedLight.CurrentLineColor = "#EEE8D5"
            lSolarizedLight.LineNumberColor = "#93A1A1"
            lSolarizedLight.LineNumberBackgroundColor = "#FDF6E3"
            lSolarizedLight.CurrentLineNumberColor = "#586E75"
            lSolarizedLight.CursorColor = "#D33682"
            lSolarizedLight.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#859900"
            lSolarizedLight.SyntaxColors(SyntaxColorSet.Tags.eType) = "#268BD2"
            lSolarizedLight.SyntaxColors(SyntaxColorSet.Tags.eString) = "#2AA198"
            lSolarizedLight.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#93A1A1"
            lSolarizedLight.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#6C71C4"
            lSolarizedLight.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#657B83"
            lSolarizedLight.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#EEE8D5"
            lThemes.Add(lSolarizedLight)
            
            ' Dracula theme
            Dim lDracula As New EditorTheme("Dracula")
            lDracula.Description = "Dark theme for developers"
            lDracula.IsDarkTheme = True
            lDracula.BackgroundColor = "#282A36"
            lDracula.ForegroundColor = "#F8F8F2"
            lDracula.SelectionColor = "#44475A"
            lDracula.CurrentLineColor = "#44475A"
            lDracula.LineNumberColor = "#6272A4"
            lDracula.LineNumberBackgroundColor = "#282A36"
            lDracula.CurrentLineNumberColor = "#F8F8F2"
            lDracula.CursorColor = "#F8F8F2"
            lDracula.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#FF79C6"
            lDracula.SyntaxColors(SyntaxColorSet.Tags.eType) = "#8BE9FD"
            lDracula.SyntaxColors(SyntaxColorSet.Tags.eString) = "#F1FA8C"
            lDracula.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#6272A4"
            lDracula.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#BD93F9"
            lDracula.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#F8F8F2"
            lDracula.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#44475A"
            lThemes.Add(lDracula)
            
            ' GitHub Dark theme
            Dim lGitHubDark As New EditorTheme("GitHub Dark")
            lGitHubDark.Description = "GitHub's dark theme"
            lGitHubDark.IsDarkTheme = True
            lGitHubDark.BackgroundColor = "#0D1117"
            lGitHubDark.ForegroundColor = "#C9D1D9"
            lGitHubDark.SelectionColor = "#1F6FEB"
            lGitHubDark.CurrentLineColor = "#161B22"
            lGitHubDark.LineNumberColor = "#8B949E"
            lGitHubDark.LineNumberBackgroundColor = "#0D1117"
            lGitHubDark.CurrentLineNumberColor = "#C9D1D9"
            lGitHubDark.CursorColor = "#C9D1D9"
            lGitHubDark.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#FF7B72"
            lGitHubDark.SyntaxColors(SyntaxColorSet.Tags.eType) = "#79C0FF"
            lGitHubDark.SyntaxColors(SyntaxColorSet.Tags.eString) = "#A5D6FF"
            lGitHubDark.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#8B949E"
            lGitHubDark.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#79C0FF"
            lGitHubDark.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#C9D1D9"
            lGitHubDark.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#1F6FEB"
            lThemes.Add(lGitHubDark)
            
            ' One Dark theme
            Dim lOneDark As New EditorTheme("One Dark")
            lOneDark.Description = "Atom One Dark theme"
            lOneDark.IsDarkTheme = True
            lOneDark.BackgroundColor = "#282C34"
            lOneDark.ForegroundColor = "#ABB2BF"
            lOneDark.SelectionColor = "#3E4451"
            lOneDark.CurrentLineColor = "#2C323C"
            lOneDark.LineNumberColor = "#636D83"
            lOneDark.LineNumberBackgroundColor = "#282C34"
            lOneDark.CurrentLineNumberColor = "#ABB2BF"
            lOneDark.CursorColor = "#528BFF"
            lOneDark.SyntaxColors(SyntaxColorSet.Tags.eKeyword) = "#C678DD"
            lOneDark.SyntaxColors(SyntaxColorSet.Tags.eType) = "#E06C75"
            lOneDark.SyntaxColors(SyntaxColorSet.Tags.eString) = "#98C379"
            lOneDark.SyntaxColors(SyntaxColorSet.Tags.eComment) = "#5C6370"
            lOneDark.SyntaxColors(SyntaxColorSet.Tags.eNumber) = "#D19A66"
            lOneDark.SyntaxColors(SyntaxColorSet.Tags.eIdentifier) = "#ABB2BF"
            lOneDark.SyntaxColors(SyntaxColorSet.Tags.eSelection) = "#3E4451"
            lThemes.Add(lOneDark)
            
            Return lThemes
        End Function
        
        ' Load custom themes from user directory
        Private Sub LoadCustomThemes()
            Try
                Dim lThemesDir As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE", "Themes")
                
                If Directory.Exists(lThemesDir) Then
                    ' Load .json files
                    for each lThemeFile in Directory.GetFiles(lThemesDir, "*.json")
                        Try
                            Dim lTheme As EditorTheme = LoadThemeFromFile(lThemeFile)
                            If lTheme IsNot Nothing Then
                                pCustomThemes.Add(lTheme)
                                pAvailableThemes(lTheme.Name) = lTheme
                            End If
                        Catch ex As Exception
                            Console.WriteLine($"error loading theme file {lThemeFile}: {ex.Message}")
                        End Try
                    Next
                    
                    ' Also load .theme files for compatibility
                    for each lThemeFile in Directory.GetFiles(lThemesDir, "*.theme")
                        Try
                            Dim lTheme As EditorTheme = LoadThemeFromFile(lThemeFile)
                            If lTheme IsNot Nothing Then
                                pCustomThemes.Add(lTheme)
                                pAvailableThemes(lTheme.Name) = lTheme
                            End If
                        Catch ex As Exception
                            Console.WriteLine($"error loading theme file {lThemeFile}: {ex.Message}")
                        End Try
                    Next
                End If
                
                Console.WriteLine($"loaded {pCustomThemes.Count} custom themes")
                
            Catch ex As Exception
                Console.WriteLine($"LoadCustomThemes error: {ex.Message}")
            End Try
        End Sub
        
        ' Load theme from file
        Private Function LoadThemeFromFile(vFilePath As String) As EditorTheme
            Try
                Dim lJson As String = File.ReadAllText(vFilePath)
                Dim lThemeData As ThemeData = JsonSerializer.Deserialize(Of ThemeData)(lJson)
                
                If lThemeData Is Nothing Then Return Nothing
                
                Dim lTheme As New EditorTheme(lThemeData.Name)
                lTheme.Description = lThemeData.Description
                lTheme.IsDarkTheme = lThemeData.IsDarkTheme
                lTheme.BackgroundColor = lThemeData.BackgroundColor
                lTheme.ForegroundColor = lThemeData.ForegroundColor
                lTheme.SelectionColor = lThemeData.SelectionColor
                lTheme.CurrentLineColor = lThemeData.CurrentLineColor
                lTheme.LineNumberColor = lThemeData.LineNumberColor
                lTheme.LineNumberBackgroundColor = lThemeData.LineNumberBackgroundColor
                lTheme.CurrentLineNumberColor = lThemeData.CurrentLineNumberColor
                lTheme.CursorColor = lThemeData.CursorColor
                lTheme.FontFamily = lThemeData.FontFamily
                lTheme.FontSize = lThemeData.FontSize
                
                ' Load syntax colors
                If lThemeData.SyntaxColors IsNot Nothing Then
                    for each kvp in lThemeData.SyntaxColors
                        Dim lTag As SyntaxColorSet.Tags
                        If [Enum].TryParse(Of SyntaxColorSet.Tags)(kvp.key, lTag) Then
                            lTheme.SyntaxColors(lTag) = kvp.Value
                        End If
                    Next
                End If
                
                Return lTheme
                
            Catch ex As Exception
                Console.WriteLine($"LoadThemeFromFile error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Save theme to file
        Public Function SaveTheme(vTheme As EditorTheme, vFilePath As String) As Boolean
            Try
                Dim lThemeData As New ThemeData()
                lThemeData.Name = vTheme.Name
                lThemeData.Description = vTheme.Description
                lThemeData.IsDarkTheme = vTheme.IsDarkTheme
                lThemeData.BackgroundColor = vTheme.BackgroundColor
                lThemeData.ForegroundColor = vTheme.ForegroundColor
                lThemeData.SelectionColor = vTheme.SelectionColor
                lThemeData.CurrentLineColor = vTheme.CurrentLineColor
                lThemeData.LineNumberColor = vTheme.LineNumberColor
                lThemeData.LineNumberBackgroundColor = vTheme.LineNumberBackgroundColor
                lThemeData.CurrentLineNumberColor = vTheme.CurrentLineNumberColor
                lThemeData.CursorColor = vTheme.CursorColor
                lThemeData.FontFamily = vTheme.FontFamily
                lThemeData.FontSize = vTheme.FontSize
                
                ' Save syntax colors
                lThemeData.SyntaxColors = New Dictionary(Of String, String)()
                for each kvp in vTheme.SyntaxColors
                    lThemeData.SyntaxColors(kvp.key.ToString()) = kvp.Value
                Next
                
                Dim lOptions As New JsonSerializerOptions()
                lOptions.WriteIndented = True
                
                Dim lJson As String = JsonSerializer.Serialize(lThemeData, lOptions)
                File.WriteAllText(vFilePath, lJson)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SaveTheme error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Create custom theme
        Public Function CreateCustomTheme(vBasedOn As String, vNewName As String) As EditorTheme
            Try
                If Not pAvailableThemes.ContainsKey(vBasedOn) Then
                    Return Nothing
                End If
                
                Dim lBaseTheme As EditorTheme = pAvailableThemes(vBasedOn)
                Dim lNewTheme As EditorTheme = lBaseTheme.Clone()
                lNewTheme.Name = vNewName
                
                ' Add to available themes
                pAvailableThemes(vNewName) = lNewTheme
                pCustomThemes.Add(lNewTheme)
                
                ' Save to file
                Dim lThemesDir As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE", "Themes")
                
                If Not Directory.Exists(lThemesDir) Then
                    Directory.CreateDirectory(lThemesDir)
                End If
                
                Dim lFilePath As String = System.IO.Path.Combine(lThemesDir, $"{vNewName}.json")
                SaveTheme(lNewTheme, lFilePath)
                
                RaiseEvent ThemeListChanged()
                Return lNewTheme
                
            Catch ex As Exception
                Console.WriteLine($"CreateCustomTheme error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Import theme from file
        Public Function ImportTheme(vFilePath As String) As EditorTheme
            Try
                Dim lTheme As EditorTheme = LoadThemeFromFile(vFilePath)
                If lTheme Is Nothing Then Return Nothing
                
                ' Check if theme name already exists
                If pAvailableThemes.ContainsKey(lTheme.Name) Then
                    ' Generate unique name
                    Dim lCounter As Integer = 1
                    Dim lNewName As String = lTheme.Name
                    While pAvailableThemes.ContainsKey(lNewName)
                        lNewName = $"{lTheme.Name} ({lCounter})"
                        lCounter += 1
                    End While
                    lTheme.Name = lNewName
                End If
                
                ' Add to themes
                pAvailableThemes(lTheme.Name) = lTheme
                pCustomThemes.Add(lTheme)
                
                ' Save to user themes directory
                Dim lThemesDir As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE", "Themes")
                
                If Not Directory.Exists(lThemesDir) Then
                    Directory.CreateDirectory(lThemesDir)
                End If
                
                Dim lDestPath As String = System.IO.Path.Combine(lThemesDir, $"{lTheme.Name}.json")
                SaveTheme(lTheme, lDestPath)
                
                RaiseEvent ThemeListChanged()
                Return lTheme
                
            Catch ex As Exception
                Console.WriteLine($"ImportTheme error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' Delete custom theme
        Public Function DeleteTheme(vThemeName As String) As Boolean
            Try
                ' Cannot delete built-in themes
                If Not pCustomThemes.any(Function(t) t.Name = vThemeName) Then
                    Return False
                End If
                
                ' Remove from collections
                If pAvailableThemes.ContainsKey(vThemeName) Then
                    pAvailableThemes.Remove(vThemeName)
                End If
                
                pCustomThemes.RemoveAll(Function(t) t.Name = vThemeName)
                
                ' Delete file
                Dim lThemesDir As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE", "Themes")
                
                Dim lFilePath As String = System.IO.Path.Combine(lThemesDir, $"{vThemeName}.json")
                If File.Exists(lFilePath) Then
                    File.Delete(lFilePath)
                End If
                
                ' Also check for .theme file
                lFilePath = System.IO.Path.Combine(lThemesDir, $"{vThemeName}.theme")
                If File.Exists(lFilePath) Then
                    File.Delete(lFilePath)
                End If
                
                ' If deleted theme was current, switch to default
                If pCurrentTheme IsNot Nothing AndAlso pCurrentTheme.Name = vThemeName Then
                    SetTheme("Default Dark")
                End If
                
                RaiseEvent ThemeListChanged()
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"DeleteTheme error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get theme by name (compatibility)
        Public Function GetThemeCss(vThemeName As String) As String
            Try
                Dim lTheme As EditorTheme = GetTheme(vThemeName)
                If lTheme IsNot Nothing Then
                    Return GenerateThemeCss(lTheme)
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetThemeCss error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' Get the current editor theme for applying to editors
        Public Function GetEditorTheme() As EditorTheme
            Try
                ' Return current theme if available
                If pCurrentTheme IsNot Nothing Then
                    Return pCurrentTheme
                End If
                
                ' If no current theme, try to get the default
                If pAvailableThemes.ContainsKey("Default Dark") Then
                    Return pAvailableThemes("Default Dark")
                End If
                
                ' If no default, return the first available theme
                If pAvailableThemes.Count > 0 Then
                    Return pAvailableThemes.Values.First()
                End If
                
                ' Last resort: create a basic default theme
                Console.WriteLine("GetEditorTheme: No themes available, creating default")
                Dim lDefaultTheme As New EditorTheme("Default Dark")
                pAvailableThemes("Default Dark") = lDefaultTheme
                pCurrentTheme = lDefaultTheme
                Return lDefaultTheme
                
            Catch ex As Exception
                Console.WriteLine($"GetEditorTheme error: {ex.Message}")
                
                ' Return a basic theme on error
                Return New EditorTheme("Fallback Dark")
            End Try
        End Function

        ' New method to remove all theme providers
        Private Sub RemoveAllThemeProviders()
            Try
                ' Remove the current provider if it exists
                If pCssProvider IsNot Nothing Then
                    StyleContext.RemoveProviderForScreen(Gdk.Screen.Default, pCssProvider)
                    pCssProvider = Nothing
                End If
                
                ' Note: We can't remove other providers without references to them,
                ' but setting a new one with USER priority should override them
                
            Catch ex As Exception
                Console.WriteLine($"RemoveAllThemeProviders error: {ex.Message}")
            End Try
        End Sub
        
        ' New method to force global widget refresh
        Private Sub ForceGlobalRefresh()
            Try
                ' Get all toplevel windows and refresh them
                Dim lWindows As Window() = Window.ListToplevels()
                For Each lWindow As Window In lWindows
                    If lWindow IsNot Nothing AndAlso lWindow.Visible Then
                        ' Reset style context to force re-evaluation
                        lWindow.ResetStyle()
                        
                        ' Queue redraw
                        lWindow.QueueDraw()
                        
                        ' Also refresh all children recursively
                        RefreshWidgetRecursive(lWindow)
                    End If
                Next
                
                ' Process pending events to ensure updates are applied
                While Application.EventsPending()
                    Application.RunIteration(False)
                End While
                
            Catch ex As Exception
                Console.WriteLine($"ForceGlobalRefresh error: {ex.Message}")
            End Try
        End Sub

        ' Recursive helper to refresh all widgets
        Private Sub RefreshWidgetRecursive(vWidget As Widget)
            Try
                If vWidget Is Nothing Then Return
                
                ' Reset the widget's style
                vWidget.ResetStyle()
                vWidget.QueueDraw()
                
                ' If it's a container, refresh all children
                Dim lContainer As Container = TryCast(vWidget, Container)
                If lContainer IsNot Nothing Then
                    For Each lChild As Widget In lContainer.Children
                        RefreshWidgetRecursive(lChild)
                    Next
                End If
                
                ' Special handling for Notebook widgets
                Dim lNotebook As Notebook = TryCast(vWidget, Notebook)
                If lNotebook IsNot Nothing Then
                    For i As Integer = 0 To lNotebook.NPages - 1
                        Dim lPage As Widget = lNotebook.GetNthPage(i)
                        If lPage IsNot Nothing Then
                            RefreshWidgetRecursive(lPage)
                        End If
                    Next
                End If
                
                ' Special handling for Paned widgets
                Dim lPaned As Paned = TryCast(vWidget, Paned)
                If lPaned IsNot Nothing Then
                    If lPaned.Child1 IsNot Nothing Then
                        RefreshWidgetRecursive(lPaned.Child1)
                    End If
                    If lPaned.Child2 IsNot Nothing Then
                        RefreshWidgetRecursive(lPaned.Child2)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RefreshWidgetRecursive error: {ex.Message}")
            End Try
        End Sub
        
        ' Theme data class for JSON serialization
        Private Class ThemeData
            Public Property Name As String
            Public Property Description As String
            Public Property IsDarkTheme As Boolean
            Public Property BackgroundColor As String
            Public Property ForegroundColor As String
            Public Property SelectionColor As String
            Public Property CurrentLineColor As String
            Public Property LineNumberColor As String
            Public Property LineNumberBackgroundColor As String
            Public Property CurrentLineNumberColor As String
            Public Property CursorColor As String
            Public Property FontFamily As String
            Public Property FontSize As Integer
            Public Property SyntaxColors As Dictionary(Of String, String)
        End Class
        
    End Class
    
End Namespace
