' Managers/SettingsManager.vb - Centralized settings management for SimpleIDE (Fixed for ApplicationSettings)
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Managers
    
    Public Class SettingsManager
        Implements IDisposable
        
        ' ===== Constants =====
        Private Const MAX_RECENT_FILES As Integer = 10
        Private Const MAX_RECENT_PROJECTS As Integer = 5
        Private Const SETTINGS_SEPARATOR As String = "|"
        
        ' ===== Events =====
        Public Event SettingsChanged(vSettingName As String, vOldValue As Object, vNewValue As Object)
        Public Event ThemeChanged(vThemeName As String)
        Public Event RecentFilesChanged()
        Public Event RecentProjectsChanged()
        
        ' ===== Private Fields =====
        Private pIsInitialized As Boolean = False
        Private pRecentFiles As New List(Of String)()
        Private pRecentProjects As New List(Of String)()
        
        ' ===== Constructor =====
        Public Sub New()
            Try
                InitializeSettings()
                LoadRecentFiles()
                LoadRecentProjects()
                pIsInitialized = True
                
            Catch ex As Exception
                Console.WriteLine($"SettingsManager constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Initialization =====
        Private Sub InitializeSettings()
            Try
                ' Load settings from ApplicationSettings - this initializes the settings system
                ' The settings will be automatically loaded from the user's settings file
                Console.WriteLine("SettingsManager: Initializing settings system")
                
                ' Validate and fix any invalid settings
                ValidateSettings()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeSettings error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ValidateSettings()
            Try
                ' Get the application settings instance
                Dim lSettings As ApplicationSettings = ApplicationSettings.Instance
                
                ' Validate window dimensions
                If lSettings.WindowWidth < 400 Then lSettings.WindowWidth = 1200
                If lSettings.WindowHeight < 300 Then lSettings.WindowHeight = 800
                If lSettings.LeftPanelWidth < 100 Then lSettings.LeftPanelWidth = 250
                If lSettings.BottomPanelHeight < 50 Then lSettings.BottomPanelHeight = 200
                
                ' Validate tab width
                If lSettings.TabWidth < 1 OrElse lSettings.TabWidth > 16 Then
                    lSettings.TabWidth = 4
                End If
                
                ' Validate IntelliSense delay
                If lSettings.IntelliSenseDelay < 100 OrElse lSettings.IntelliSenseDelay > 5000 Then
                    lSettings.IntelliSenseDelay = 500
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ValidateSettings error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Settings Methods =====
        
        ' Save all settings to disk
        Public Sub SaveSettings()
            Try
                ' Save the ApplicationSettings to disk
                ApplicationSettings.Instance.SaveSettings()
                
                ' Save recent files and projects
                SaveRecentFiles()
                SaveRecentProjects()
                
                Console.WriteLine("Settings saved successfully")
                
            Catch ex As Exception
                Console.WriteLine($"SaveSettings error: {ex.Message}")
            End Try
        End Sub
        
        ' Save method alias for compatibility
        Public Sub Save()
            SaveSettings()
        End Sub
        
        ' Reset all settings to defaults
        Public Sub ResetToDefaults()
            Try
                ' Reset ApplicationSettings to defaults
                ApplicationSettings.Instance.ResetToDefaults()
                
                ' Clear recent files and projects
                pRecentFiles.Clear()
                pRecentProjects.Clear()
                SaveRecentFiles()
                SaveRecentProjects()
                
                ' Reload settings
                InitializeSettings()
                LoadRecentFiles()
                LoadRecentProjects()
                
                Console.WriteLine("Settings reset to defaults")
                
            Catch ex As Exception
                Console.WriteLine($"ResetToDefaults error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Get Setting Methods =====
        
        ' Get string setting with default value
        Public Function GetSetting(vKey As String, Optional vDefaultValue As String = "") As String
            Try
                ' Map key to ApplicationSettings property or use generic storage
                Select Case vKey.ToLower()
                    Case "EditorFont" : Return EditorFont
                    Case "ColorTheme" : Return ColorTheme
                    Case "CurrentTheme" : Return CurrentTheme
                    Case "BuildConfiguration" : Return BuildConfiguration
                    Case "BuildPlatform" : Return BuildPlatform
                    Case "LastProjectPath" : Return LastProjectPath
                    Case "LastFilePath" : Return LastFilePath
                    Case "GitUserName" : Return GitUserName
                    Case "GitEmail" : Return GitEmail
                    Case "GitDefaultBranch" : Return GitDefaultBranch
                    Case Else
                        ' Generic key-value storage
                        Return GetCustomSetting(vKey, vDefaultValue)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetSetting error for key '{vKey}': {ex.Message}")
                Return vDefaultValue
            End Try
        End Function
        
        ' Get string setting - alias for compatibility
        Public Function GetString(vKey As String, Optional vDefaultValue As String = "") As String
            Return GetSetting(vKey, vDefaultValue)
        End Function
        
        ' Get boolean setting with default value
        Public Function GetBooleanSetting(vKey As String, Optional vDefaultValue As Boolean = False) As Boolean
            Try
                Select Case vKey.ToLower()
                    Case "WindowMaximized" : Return WindowMaximized
                    Case "ShowStatusBar" : Return ShowStatusBar
                    Case "ShowToolbar" : Return ShowToolbar
                    Case "ShowProjectExplorer" : Return ShowProjectExplorer
                    Case "ShowBottomPanel" : Return ShowBottomPanel
                    Case "ShowLineNumbers" : Return ShowLineNumbers
                    Case "WordWrap" : Return WordWrap
                    Case "ShowWhitespace" : Return ShowWhitespace
                    Case "BraceMatching" : Return BraceMatching
                    Case "EnableLogging" : Return EnableLogging
                    Case "SaveWindowLayout" : Return SaveWindowLayout
                    Case "AutoSave" : Return AutoSave
                    Case "BuildBeforeRun" : Return BuildBeforeRun
                    Case "ClearOutputOnBuild" : Return ClearOutputOnBuild
                    Case "ShowBuildOutput" : Return ShowBuildOutput
                    Case "AIAssistantEnabled" : Return AIAssistantEnabled
                    Case "IntelliSenseEnabled" : Return IntelliSenseEnabled
                    Case "FindCaseSensitive" : Return FindCaseSensitive
                    Case "FindWholeWord" : Return FindWholeWord
                    Case "FindUseRegex" : Return FindUseRegex
                    Case "HighlightCurrentLine" : Return HighlightCurrentLine
                    Case "AutoIndent" : Return AutoIndent
                    Case Else
                        ' Generic boolean setting
                        Dim lStringValue As String = GetCustomSetting(vKey, vDefaultValue.ToString())
                        Return Boolean.TryParse(lStringValue, Nothing) AndAlso Boolean.Parse(lStringValue)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetBooleanSetting error for key '{vKey}': {ex.Message}")
                Return vDefaultValue
            End Try
        End Function
        
        ' Get boolean setting - alias for compatibility
        Public Function GetBoolean(vKey As String, Optional vDefaultValue As Boolean = False) As Boolean
            Return GetBooleanSetting(vKey, vDefaultValue)
        End Function
        
        ' Get integer setting with default value
        Public Function GetIntegerSetting(vKey As String, Optional vDefaultValue As Integer = 0) As Integer
            Try
                Select Case vKey.ToLower()
                    Case "WindowWidth" : Return WindowWidth
                    Case "WindowHeight" : Return WindowHeight
                    Case "LeftPanelWidth" : Return LeftPanelWidth
                    Case "BottomPanelHeight" : Return BottomPanelHeight
                    Case "TabWidth" : Return TabWidth
                    Case "IntelliSenseDelay" : Return IntelliSenseDelay
                    Case Else
                        ' Generic integer setting
                        Dim lStringValue As String = GetCustomSetting(vKey, vDefaultValue.ToString())
                        Dim lResult As Integer
                        Return If(Integer.TryParse(lStringValue, lResult), lResult, vDefaultValue)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"GetIntegerSetting error for key '{vKey}': {ex.Message}")
                Return vDefaultValue
            End Try
        End Function
        
        ' Get integer setting - alias for compatibility
        Public Function GetInteger(vKey As String, Optional vDefaultValue As Integer = 0) As Integer
            Return GetIntegerSetting(vKey, vDefaultValue)
        End Function
        
        ' Get double setting with default value
        Public Function GetDoubleSetting(vKey As String, Optional vDefaultValue As Double = 0.0) As Double
            Try
                ' Generic double setting
                Dim lStringValue As String = GetCustomSetting(vKey, vDefaultValue.ToString())
                Dim lResult As Double
                Return If(Double.TryParse(lStringValue, lResult), lResult, vDefaultValue)
                
            Catch ex As Exception
                Console.WriteLine($"GetDoubleSetting error for key '{vKey}': {ex.Message}")
                Return vDefaultValue
            End Try
        End Function
        
        ' Get double setting - alias for compatibility
        Public Function GetDouble(vKey As String, Optional vDefaultValue As Double = 0.0) As Double
            Return GetDoubleSetting(vKey, vDefaultValue)
        End Function
        
        ' ===== Set Setting Methods =====
        
        ' Set string setting value
        Public Sub SetSetting(vKey As String, vValue As String)
            Try
                ' Map key to ApplicationSettings property or use generic storage
                Select Case vKey.ToLower()
                    Case "EditorFont" : EditorFont = vValue
                    Case "ColorTheme" : ColorTheme = vValue
                    Case "CurrentTheme" : CurrentTheme = vValue
                    Case "BuildConfiguration" : BuildConfiguration = vValue
                    Case "BuildPlatform" : BuildPlatform = vValue
                    Case Else
                        ' Generic key-value storage
                        SetCustomSetting(vKey, vValue)
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"SetSetting error for key '{vKey}': {ex.Message}")
            End Try
        End Sub
        
        ' Set string setting - alias for compatibility
        Public Sub SetString(vKey As String, vValue As String)
            SetSetting(vKey, vValue)
        End Sub
        
        ' Set integer setting
        Public Sub SetInteger(vKey As String, vValue As Integer)
            Try
                Select Case vKey.ToLower()
                    Case "WindowWidth" : WindowWidth = vValue
                    Case "WindowHeight" : WindowHeight = vValue
                    Case "LeftPanelWidth" : LeftPanelWidth = vValue
                    Case "BottomPanelHeight" : BottomPanelHeight = vValue
                    Case "TabWidth" : TabWidth = vValue
                    Case "IntelliSenseDelay" : IntelliSenseDelay = vValue
                    Case Else
                        ' Generic setting - store as string
                        SetCustomSetting(vKey, vValue.ToString())
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"SetInteger error for key '{vKey}': {ex.Message}")
            End Try
        End Sub
        
        ' Set double setting
        Public Sub SetDouble(vKey As String, vValue As Double)
            Try
                ' Generic setting - store as string
                SetCustomSetting(vKey, vValue.ToString())
                
            Catch ex As Exception
                Console.WriteLine($"SetDouble error for key '{vKey}': {ex.Message}")
            End Try
        End Sub

        Public ReadOnly Property RecentProjects As List(Of String)
            Get
                Return pRecentProjects
            End Get
        End Property
        
        ' Set boolean setting
        Public Sub SetBoolean(vKey As String, vValue As Boolean)
            Try
                Select Case vKey.ToLower()
                    Case "WindowMaximized" : WindowMaximized = vValue
                    Case "ShowStatusBar" : ShowStatusBar = vValue
                    Case "ShowToolbar" : ShowToolbar = vValue
                    Case "ShowProjectExplorer" : ShowProjectExplorer = vValue
                    Case "ShowBottomPanel" : ShowBottomPanel = vValue
                    Case "ShowLineNumbers" : ShowLineNumbers = vValue
                    Case "WordWrap" : WordWrap = vValue
                    Case "ShowWhitespace" : ShowWhitespace = vValue
                    Case "BraceMatching" : BraceMatching = vValue
                    Case "EnableLogging" : EnableLogging = vValue
                    Case "SaveWindowLayout" : SaveWindowLayout = vValue
                    Case "AutoSave" : AutoSave = vValue
                    Case "BuildBeforeRun" : BuildBeforeRun = vValue
                    Case "ClearOutputOnBuild" : ClearOutputOnBuild = vValue
                    Case "ShowBuildOutput" : ShowBuildOutput = vValue
                    Case "AIAssistantEnabled" : AIAssistantEnabled = vValue
                    Case "IntelliSenseEnabled" : IntelliSenseEnabled = vValue
                    Case "FindCaseSensitive" : FindCaseSensitive = vValue
                    Case "FindWholeWord" : FindWholeWord = vValue
                    Case "FindUseRegex" : FindUseRegex = vValue
                    Case "HighlightCurrentLine" : HighlightCurrentLine = vValue
                    Case "AutoIndent" : AutoIndent = vValue
                    Case "ToolbarShowLabels" : ToolbarShowLabels = vValue
                    Case "ToolbarLargeIcons" : ToolbarLargeIcons = vValue
                    Case Else
                        ' Generic setting - store as string
                        SetCustomSetting(vKey, vValue.ToString())
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"SetBoolean error for key '{vKey}': {ex.Message}")
            End Try
        End Sub
        
        ' ===== Custom Settings Storage =====
        
        Private Function GetCustomSetting(vKey As String, vDefaultValue As String) As String
            Try
                ' Use ApplicationSettings.CustomSettings as a delimited string for generic storage
                Dim lCustomSettings As String = ApplicationSettings.Instance.CustomSettings
                If String.IsNullOrEmpty(lCustomSettings) Then Return vDefaultValue
                
                Dim lPairs As String() = lCustomSettings.Split({"|"c}, StringSplitOptions.RemoveEmptyEntries)
                For Each lPair In lPairs
                    Dim lKeyValue As String() = lPair.Split({"="c}, 2)
                    If lKeyValue.Length = 2 AndAlso lKeyValue(0).Equals(vKey, StringComparison.OrdinalIgnoreCase) Then
                        Return lKeyValue(1)
                    End If
                Next
                
                Return vDefaultValue
                
            Catch ex As Exception
                Console.WriteLine($"GetCustomSetting error for key '{vKey}': {ex.Message}")
                Return vDefaultValue
            End Try
        End Function
        
        Private Sub SetCustomSetting(vKey As String, vValue As String)
            Try
                Dim lSettings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                
                ' Load existing settings
                Dim lCustomSettings As String = ApplicationSettings.Instance.CustomSettings
                If Not String.IsNullOrEmpty(lCustomSettings) Then
                    Dim lPairs As String() = lCustomSettings.Split({"|"c}, StringSplitOptions.RemoveEmptyEntries)
                    For Each lPair In lPairs
                        Dim lKeyValue As String() = lPair.Split({"="c}, 2)
                        If lKeyValue.Length = 2 Then
                            lSettings(lKeyValue(0)) = lKeyValue(1)
                        End If
                    Next
                End If
                
                ' Update or add setting
                lSettings(vKey) = vValue
                
                ' Save back to ApplicationSettings
                Dim lPairList As New List(Of String)()
                For Each lKvp In lSettings
                    lPairList.Add($"{lKvp.key}={lKvp.Value}")
                Next
                
                ApplicationSettings.Instance.CustomSettings = String.Join("|", lPairList)
                
            Catch ex As Exception
                Console.WriteLine($"SetCustomSetting error for key '{vKey}': {ex.Message}")
            End Try
        End Sub
        
        ' ===== Window Settings =====
        
        Public Property WindowWidth As Integer
            Get
                Return ApplicationSettings.Instance.WindowWidth
            End Get
            Set(Value As Integer)
                Dim lOldValue As Integer = ApplicationSettings.Instance.WindowWidth
                ApplicationSettings.Instance.WindowWidth = Math.Max(400, Value)
                RaiseEvent SettingsChanged("WindowWidth", lOldValue, Value)
            End Set
        End Property
        
        Public Property WindowHeight As Integer
            Get
                Return ApplicationSettings.Instance.WindowHeight
            End Get
            Set(Value As Integer)
                Dim lOldValue As Integer = ApplicationSettings.Instance.WindowHeight
                ApplicationSettings.Instance.WindowHeight = Math.Max(300, Value)
                RaiseEvent SettingsChanged("WindowHeight", lOldValue, Value)
            End Set
        End Property
        
        Public Property WindowMaximized As Boolean
            Get
                Return ApplicationSettings.Instance.WindowMaximized
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.WindowMaximized
                ApplicationSettings.Instance.WindowMaximized = Value
                RaiseEvent SettingsChanged("WindowMaximized", lOldValue, Value)
            End Set
        End Property
        
        Public Property LeftPanelWidth As Integer
            Get
                Return ApplicationSettings.Instance.LeftPanelWidth
            End Get
            Set(Value As Integer)
                Dim lOldValue As Integer = ApplicationSettings.Instance.LeftPanelWidth
                ApplicationSettings.Instance.LeftPanelWidth = Math.Max(100, Value)
                RaiseEvent SettingsChanged("LeftPanelWidth", lOldValue, Value)
            End Set
        End Property
        
        Public Property BottomPanelHeight As Integer
            Get
                Return ApplicationSettings.Instance.BottomPanelHeight
            End Get
            Set(Value As Integer)
                Dim lOldValue As Integer = ApplicationSettings.Instance.BottomPanelHeight
                ApplicationSettings.Instance.BottomPanelHeight = Math.Max(50, Value)
                RaiseEvent SettingsChanged("BottomPanelHeight", lOldValue, Value)
            End Set
        End Property
        
        ' ===== Editor Settings =====
        
        Public Property UseTabs As Boolean
            Get
                Return ApplicationSettings.Instance.UseTabs
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.UseTabs
                ApplicationSettings.Instance.UseTabs = Value
                RaiseEvent SettingsChanged("UseTabs", lOldValue, Value)
            End Set
        End Property

        Public Property EditorFont As String
            Get
                Return ApplicationSettings.Instance.EditorFont
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.EditorFont
                ApplicationSettings.Instance.EditorFont = Value
                RaiseEvent SettingsChanged("EditorFont", lOldValue, Value)
            End Set
        End Property
        
        Public Property TabWidth As Integer
            Get
                Return ApplicationSettings.Instance.TabWidth
            End Get
            Set(Value As Integer)
                Dim lOldValue As Integer = ApplicationSettings.Instance.TabWidth
                ApplicationSettings.Instance.TabWidth = Math.Max(1, Math.Min(16, Value))
                RaiseEvent SettingsChanged("TabWidth", lOldValue, Value)
            End Set
        End Property
        
        Public Property ShowLineNumbers As Boolean
            Get
                Return ApplicationSettings.Instance.ShowLineNumbers
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ShowLineNumbers
                ApplicationSettings.Instance.ShowLineNumbers = Value
                RaiseEvent SettingsChanged("ShowLineNumbers", lOldValue, Value)
            End Set
        End Property
        
        Public Property WordWrap As Boolean
            Get
                Return ApplicationSettings.Instance.WordWrap
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.WordWrap
                ApplicationSettings.Instance.WordWrap = Value
                RaiseEvent SettingsChanged("WordWrap", lOldValue, Value)
            End Set
        End Property
        
        Public Property ShowWhitespace As Boolean
            Get
                Return ApplicationSettings.Instance.ShowWhitespace
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ShowWhitespace
                ApplicationSettings.Instance.ShowWhitespace = Value
                RaiseEvent SettingsChanged("ShowWhitespace", lOldValue, Value)
            End Set
        End Property
        
        Public Property BraceMatching As Boolean
            Get
                Return ApplicationSettings.Instance.BraceMatching
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.BraceMatching
                ApplicationSettings.Instance.BraceMatching = Value
                RaiseEvent SettingsChanged("BraceMatching", lOldValue, Value)
            End Set
        End Property
        
        Public Property HighlightCurrentLine As Boolean
            Get
                Return ApplicationSettings.Instance.HighlightCurrentLine
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.HighlightCurrentLine
                ApplicationSettings.Instance.HighlightCurrentLine = Value
                RaiseEvent SettingsChanged("HighlightCurrentLine", lOldValue, Value)
            End Set
        End Property
        
        Public Property AutoIndent As Boolean
            Get
                Return ApplicationSettings.Instance.AutoIndent
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.AutoIndent
                ApplicationSettings.Instance.AutoIndent = Value
                RaiseEvent SettingsChanged("AutoIndent", lOldValue, Value)
            End Set
        End Property
        
        ' ===== Theme Settings =====
        
        Public Property ColorTheme As String
            Get
                Return ApplicationSettings.Instance.ColorTheme
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.ColorTheme
                ApplicationSettings.Instance.ColorTheme = Value
                RaiseEvent SettingsChanged("ColorTheme", lOldValue, Value)
                RaiseEvent ThemeChanged(Value)
            End Set
        End Property
        
        Public Property CurrentTheme As String
            Get
                Return ApplicationSettings.Instance.CurrentTheme
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.CurrentTheme
                ApplicationSettings.Instance.CurrentTheme = Value
                RaiseEvent SettingsChanged("CurrentTheme", lOldValue, Value)
            End Set
        End Property
        
        ' ===== UI Settings =====
        
        Public Property ShowStatusBar As Boolean
            Get
                Return ApplicationSettings.Instance.ShowStatusBar
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ShowStatusBar
                ApplicationSettings.Instance.ShowStatusBar = Value
                RaiseEvent SettingsChanged("ShowStatusBar", lOldValue, Value)
            End Set
        End Property
        
        Public Property ShowToolbar As Boolean
            Get
                Return ApplicationSettings.Instance.ShowToolbar
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ShowToolbar
                ApplicationSettings.Instance.ShowToolbar = Value
                RaiseEvent SettingsChanged("ShowToolbar", lOldValue, Value)
            End Set
        End Property

        Public Property ToolbarShowLabels As Boolean
            Get
                Return ApplicationSettings.Instance.ToolbarShowLabels
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ToolbarShowLabels
                ApplicationSettings.Instance.ToolbarShowLabels = Value
                RaiseEvent SettingsChanged("ToolbarShowLabels", lOldValue, Value)
            End Set
        End Property
        
        Public Property ToolbarLargeIcons As Boolean
            Get
                Return ApplicationSettings.Instance.ToolbarLargeIcons
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ToolbarLargeIcons
                ApplicationSettings.Instance.ToolbarLargeIcons = Value
                RaiseEvent SettingsChanged("ToolbarLargeIcons", lOldValue, Value)
            End Set
        End Property
        
        Public Property ShowProjectExplorer As Boolean
            Get
                Return ApplicationSettings.Instance.ShowProjectExplorer
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ShowProjectExplorer
                ApplicationSettings.Instance.ShowProjectExplorer = Value
                RaiseEvent SettingsChanged("ShowProjectExplorer", lOldValue, Value)
            End Set
        End Property
        
        Public Property ShowBottomPanel As Boolean
            Get
                Return ApplicationSettings.Instance.ShowBottomPanel
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ShowBottomPanel
                ApplicationSettings.Instance.ShowBottomPanel = Value
                RaiseEvent SettingsChanged("ShowBottomPanel", lOldValue, Value)
            End Set
        End Property
        
        ' ===== Build Settings =====
        
        Public Property BuildConfiguration As String
            Get
                Return ApplicationSettings.Instance.BuildConfiguration
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.BuildConfiguration
                ApplicationSettings.Instance.BuildConfiguration = Value
                RaiseEvent SettingsChanged("BuildConfiguration", lOldValue, Value)
            End Set
        End Property
        
        Public Property BuildPlatform As String
            Get
                Return ApplicationSettings.Instance.BuildPlatform
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.BuildPlatform
                ApplicationSettings.Instance.BuildPlatform = Value
                RaiseEvent SettingsChanged("BuildPlatform", lOldValue, Value)
            End Set
        End Property
        
        Public Property BuildBeforeRun As Boolean
            Get
                Return ApplicationSettings.Instance.BuildBeforeRun
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.BuildBeforeRun
                ApplicationSettings.Instance.BuildBeforeRun = Value
                RaiseEvent SettingsChanged("BuildBeforeRun", lOldValue, Value)
            End Set
        End Property
        
        Public Property ClearOutputOnBuild As Boolean
            Get
                Return ApplicationSettings.Instance.ClearOutputOnBuild
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ClearOutputOnBuild
                ApplicationSettings.Instance.ClearOutputOnBuild = Value
                RaiseEvent SettingsChanged("ClearOutputOnBuild", lOldValue, Value)
            End Set
        End Property
        
        Public Property ShowBuildOutput As Boolean
            Get
                Return ApplicationSettings.Instance.ShowBuildOutput
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.ShowBuildOutput
                ApplicationSettings.Instance.ShowBuildOutput = Value
                RaiseEvent SettingsChanged("ShowBuildOutput", lOldValue, Value)
            End Set
        End Property
        
        ' ===== File Settings =====
        
        Public Property LastProjectPath As String
            Get
                Return ApplicationSettings.Instance.LastProjectPath
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.LastProjectPath
                ApplicationSettings.Instance.LastProjectPath = Value
                RaiseEvent SettingsChanged("LastProjectPath", lOldValue, Value)
            End Set
        End Property
        
        Public Property LastFilePath As String
            Get
                Return ApplicationSettings.Instance.LastFilePath
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.LastFilePath
                ApplicationSettings.Instance.LastFilePath = Value
                RaiseEvent SettingsChanged("LastFilePath", lOldValue, Value)
            End Set
        End Property
        
        ' ===== Git Settings =====
        
        Public Property GitUserName As String
            Get
                Return ApplicationSettings.Instance.GitUserName
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.GitUserName
                ApplicationSettings.Instance.GitUserName = Value
                RaiseEvent SettingsChanged("GitUserName", lOldValue, Value)
            End Set
        End Property
        
        Public Property GitEmail As String
            Get
                Return ApplicationSettings.Instance.GitEmail
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.GitEmail
                ApplicationSettings.Instance.GitEmail = Value
                RaiseEvent SettingsChanged("GitEmail", lOldValue, Value)
            End Set
        End Property
        
        Public Property GitDefaultBranch As String
            Get
                Return ApplicationSettings.Instance.GitDefaultBranch
            End Get
            Set(Value As String)
                Dim lOldValue As String = ApplicationSettings.Instance.GitDefaultBranch
                ApplicationSettings.Instance.GitDefaultBranch = Value
                RaiseEvent SettingsChanged("GitDefaultBranch", lOldValue, Value)
            End Set
        End Property
        
        ' ===== AI Settings =====
        
        Public Property AIAssistantEnabled As Boolean
            Get
                Return ApplicationSettings.Instance.AIAssistantEnabled
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.AIAssistantEnabled
                ApplicationSettings.Instance.AIAssistantEnabled = Value
                RaiseEvent SettingsChanged("AIAssistantEnabled", lOldValue, Value)
            End Set
        End Property
        
        ' ===== IntelliSense Settings =====
        
        Public Property IntelliSenseEnabled As Boolean
            Get
                Return ApplicationSettings.Instance.IntelliSenseEnabled
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.IntelliSenseEnabled
                ApplicationSettings.Instance.IntelliSenseEnabled = Value
                RaiseEvent SettingsChanged("IntelliSenseEnabled", lOldValue, Value)
            End Set
        End Property
        
        Public Property IntelliSenseDelay As Integer
            Get
                Return ApplicationSettings.Instance.IntelliSenseDelay
            End Get
            Set(Value As Integer)
                Dim lOldValue As Integer = ApplicationSettings.Instance.IntelliSenseDelay
                ApplicationSettings.Instance.IntelliSenseDelay = Math.Max(100, Math.Min(5000, Value))
                RaiseEvent SettingsChanged("IntelliSenseDelay", lOldValue, Value)
            End Set
        End Property
        
        ' ===== Find/Replace Settings =====
        
        Public Property FindCaseSensitive As Boolean
            Get
                Return ApplicationSettings.Instance.FindCaseSensitive
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.FindCaseSensitive
                ApplicationSettings.Instance.FindCaseSensitive = Value
                RaiseEvent SettingsChanged("FindCaseSensitive", lOldValue, Value)
            End Set
        End Property
        
        Public Property FindWholeWord As Boolean
            Get
                Return ApplicationSettings.Instance.FindWholeWord
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.FindWholeWord
                ApplicationSettings.Instance.FindWholeWord = Value
                RaiseEvent SettingsChanged("FindWholeWord", lOldValue, Value)
            End Set
        End Property
        
        Public Property FindUseRegex As Boolean
            Get
                Return ApplicationSettings.Instance.FindUseRegex
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.FindUseRegex
                ApplicationSettings.Instance.FindUseRegex = Value
                RaiseEvent SettingsChanged("FindUseRegex", lOldValue, Value)
            End Set
        End Property
        
        ' ===== System Settings =====
        
        Public Property EnableLogging As Boolean
            Get
                Return ApplicationSettings.Instance.EnableLogging
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.EnableLogging
                ApplicationSettings.Instance.EnableLogging = Value
                RaiseEvent SettingsChanged("EnableLogging", lOldValue, Value)
            End Set
        End Property
        
        Public Property SaveWindowLayout As Boolean
            Get
                Return ApplicationSettings.Instance.SaveWindowLayout
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.SaveWindowLayout
                ApplicationSettings.Instance.SaveWindowLayout = Value
                RaiseEvent SettingsChanged("SaveWindowLayout", lOldValue, Value)
            End Set
        End Property
        
        Public Property AutoSave As Boolean
            Get
                Return ApplicationSettings.Instance.AutoSave
            End Get
            Set(Value As Boolean)
                Dim lOldValue As Boolean = ApplicationSettings.Instance.AutoSave
                ApplicationSettings.Instance.AutoSave = Value
                RaiseEvent SettingsChanged("AutoSave", lOldValue, Value)
            End Set
        End Property
        
        ' ===== Recent Files Management =====
        
        ' Get recent files list
        Public Function GetRecentFiles() As List(Of String)
            Return New List(Of String)(pRecentFiles)
        End Function
        
        ' Add file to recent files
        Public Sub AddRecentFile(vFilePath As String)
            Try
                If String.IsNullOrEmpty(vFilePath) Then Return
                
                ' Remove if already exists
                pRecentFiles.Remove(vFilePath)
                
                ' Add to beginning
                pRecentFiles.Insert(0, vFilePath)
                
                ' Keep only MAX_RECENT_FILES
                While pRecentFiles.Count > MAX_RECENT_FILES
                    pRecentFiles.RemoveAt(pRecentFiles.Count - 1)
                End While
                
                SaveRecentFiles()
                RaiseEvent RecentFilesChanged()
                
            Catch ex As Exception
                Console.WriteLine($"AddRecentFile error: {ex.Message}")
            End Try
        End Sub
        
        ' Clear recent files
        Public Sub ClearRecentFiles()
            Try
                pRecentFiles.Clear()
                SaveRecentFiles()
                RaiseEvent RecentFilesChanged()
                
            Catch ex As Exception
                Console.WriteLine($"ClearRecentFiles error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub LoadRecentFiles()
            Try
                pRecentFiles.Clear()
                
                Dim lRecentFilesString As String = ApplicationSettings.Instance.RecentFiles
                If Not String.IsNullOrEmpty(lRecentFilesString) Then
                    Dim lFiles As String() = lRecentFilesString.Split({SETTINGS_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries)
                    
                    For Each lFile In lFiles
                        If File.Exists(lFile) Then
                            pRecentFiles.Add(lFile)
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadRecentFiles error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SaveRecentFiles()
            Try
                ApplicationSettings.Instance.RecentFiles = String.Join(SETTINGS_SEPARATOR, pRecentFiles)
                
            Catch ex As Exception
                Console.WriteLine($"SaveRecentFiles error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Recent Projects Management =====
        
        ' Get recent projects list
        Public Function GetRecentProjects() As List(Of String)
            Return New List(Of String)(pRecentProjects)
        End Function
        
        ' Add project to recent projects
        Public Sub AddRecentProject(vProjectPath As String)
            Try
                If String.IsNullOrEmpty(vProjectPath) Then Return
                
                ' Remove if already exists
                pRecentProjects.Remove(vProjectPath)
                
                ' Add to beginning
                pRecentProjects.Insert(0, vProjectPath)
                
                ' Keep only MAX_RECENT_PROJECTS
                While pRecentProjects.Count > MAX_RECENT_PROJECTS
                    pRecentProjects.RemoveAt(pRecentProjects.Count - 1)
                End While
                
                SaveRecentProjects()
                RaiseEvent RecentProjectsChanged()
                
            Catch ex As Exception
                Console.WriteLine($"AddRecentProject error: {ex.Message}")
            End Try
        End Sub
        
        ' Clear recent projects
        Public Sub ClearRecentProjects()
            Try
                pRecentProjects.Clear()
                SaveRecentProjects()
                RaiseEvent RecentProjectsChanged()
                
            Catch ex As Exception
                Console.WriteLine($"ClearRecentProjects error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub LoadRecentProjects()
            Try
                pRecentProjects.Clear()
                
                Dim lRecentProjectsString As String = ApplicationSettings.Instance.RecentProjects
                If Not String.IsNullOrEmpty(lRecentProjectsString) Then
                    Dim lProjects As String() = lRecentProjectsString.Split({SETTINGS_SEPARATOR}, StringSplitOptions.RemoveEmptyEntries)
                    
                    For Each lProject In lProjects
                        If File.Exists(lProject) Then
                            pRecentProjects.Add(lProject)
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadRecentProjects error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SaveRecentProjects()
            Try
                ApplicationSettings.Instance.RecentProjects = String.Join(SETTINGS_SEPARATOR, pRecentProjects)
                
            Catch ex As Exception
                Console.WriteLine($"SaveRecentProjects error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== IDisposable Implementation =====
        
        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                ' Save settings before disposing
                SaveSettings()
                
                ' Clear collections
                pRecentFiles.Clear()
                pRecentProjects.Clear()
                
                pIsInitialized = False
                
            Catch ex As Exception
                Console.WriteLine($"SettingsManager.Dispose error: {ex.Message}")
            End Try
        End Sub

        Public Property ShowVersionInTitle As Boolean
            Get
                Return GetSetting("ShowVersionInTitle", True)
            End Get
            Set(value As Boolean)
                SetSetting("ShowVersionInTitle", value)
            End Set
        End Property

        ''' <summary>
        ''' Gets or sets which tab should be shown in the left panel on startup
        ''' </summary>
        ''' <value>0 for Project tab, 1 for Objects tab</value>
        Public Property LeftPanelStartupTab As Integer
            Get
                Return ApplicationSettings.Instance.LeftPanelStartupTab
            End Get
            Set(Value As Integer)
                Dim lOldValue As Integer = ApplicationSettings.Instance.LeftPanelStartupTab
                ApplicationSettings.Instance.LeftPanelStartupTab = Value
                RaiseEvent SettingsChanged("LeftPanelStartupTab", lOldValue, Value)
            End Set
        End Property
        
    End Class
    
End Namespace

