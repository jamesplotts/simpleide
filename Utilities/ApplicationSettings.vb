' Utilities/ApplicationSettings.vb - Replacement for My.Settings functionality
Imports System
Imports System.IO
Imports System.Text.Json
Imports System.Collections.Generic

Namespace Utilities
    
    ' Provides application settings functionality similar to My.Settings
    Public Class ApplicationSettings
        
        ' ===== Private Fields =====
        Private Shared pInstance As ApplicationSettings = Nothing
        Private Shared ReadOnly pLock As New Object()
        Private pSettingsFilePath As String
        Private pSettingsData As Dictionary(Of String, Object)
        Private pIsLoaded As Boolean = False
        
        ' ===== Singleton Pattern =====
        Public Shared ReadOnly Property Instance As ApplicationSettings
            Get
                If pInstance Is Nothing Then
                    SyncLock pLock
                        If pInstance Is Nothing Then
                            pInstance = New ApplicationSettings()
                        End If
                    End SyncLock
                End If
                Return pInstance
            End Get
        End Property
        
        ' ===== Constructor =====
        Private Sub New()
            Try
                ' Set settings file path
                Dim lAppDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                Dim lAppFolder As String = Path.Combine(lAppDataPath, "SimpleIDE")
                
                If Not Directory.Exists(lAppFolder) Then
                    Directory.CreateDirectory(lAppFolder)
                End If
                
                pSettingsFilePath = Path.Combine(lAppFolder, "settings.json")
                pSettingsData = New Dictionary(Of String, Object)()
                
                ' Load existing settings
                LoadSettings()
                
            Catch ex As Exception
                Console.WriteLine($"ApplicationSettings constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Generic Property Access Methods =====
        
        ' Get a setting value with type conversion
        Private Function GetValue(Of T)(vKey As String, vDefaultValue As T) As T
            Try
                If Not pIsLoaded Then LoadSettings()
                
                If pSettingsData.ContainsKey(vKey) Then
                    Dim lValue As Object = pSettingsData(vKey)
                    
                    If lValue Is Nothing Then
                        Return vDefaultValue
                    End If
                    
                    ' Handle JsonElement conversion (from JSON deserialization)
                    If TypeOf lValue Is JsonElement Then
                        Dim lJsonElement As JsonElement = CType(lValue, JsonElement)
                        Return ConvertJsonElement(Of T)(lJsonElement, vDefaultValue)
                    End If
                    
                    ' Direct type conversion
                    If TypeOf lValue Is T Then
                        Return CType(lValue, T)
                    End If
                    
                    ' Try to convert
                    Return CType(Convert.ChangeType(lValue, GetType(T)), T)
                Else
                    Return vDefaultValue
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetValue error for key '{vKey}': {ex.Message}")
                Return vDefaultValue
            End Try
        End Function
        
        ' Set a setting value
        Private Sub SetValue(Of T)(vKey As String, vValue As T)
            Try
                If Not pIsLoaded Then LoadSettings()
                
                pSettingsData(vKey) = vValue
                
                ' Auto-save on set
                SaveSettings()
                
            Catch ex As Exception
                Console.WriteLine($"SetValue error for key '{vKey}': {ex.Message}")
            End Try
        End Sub
        
        ' ===== JSON Conversion Helper =====
        
        Private Function ConvertJsonElement(Of T)(vElement As JsonElement, vDefaultValue As T) As T
            Try
                Dim lTargetType As Type = GetType(T)
                
                Select Case lTargetType
                    Case GetType(String)
                        Return CType(CObj(vElement.GetString()), T)
                    Case GetType(Integer)
                        Return CType(CObj(vElement.GetInt32()), T)
                    Case GetType(Boolean)
                        Return CType(CObj(vElement.GetBoolean()), T)
                    Case GetType(Double)
                        Return CType(CObj(vElement.GetDouble()), T)
                    Case GetType(Single)
                        Return CType(CObj(vElement.GetSingle()), T)
                    Case Else
                        Return vDefaultValue
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ConvertJsonElement error: {ex.Message}")
                Return vDefaultValue
            End Try
        End Function
        
        ' ===== Application Settings Properties =====
        
        ' Window Settings
        Public Property WindowWidth As Integer
            Get
                Return GetValue("WindowWidth", 1200)
            End Get
            Set(Value As Integer)
                SetValue("WindowWidth", Value)
            End Set
        End Property
        
        Public Property WindowHeight As Integer
            Get
                Return GetValue("WindowHeight", 800)
            End Get
            Set(Value As Integer)
                SetValue("WindowHeight", Value)
            End Set
        End Property
        
        Public Property WindowMaximized As Boolean
            Get
                Return GetValue("WindowMaximized", False)
            End Get
            Set(Value As Boolean)
                SetValue("WindowMaximized", Value)
            End Set
        End Property
        
        Public Property LeftPanelWidth As Integer
            Get
                Return GetValue("LeftPanelWidth", 250)
            End Get
            Set(Value As Integer)
                SetValue("LeftPanelWidth", Value)
            End Set
        End Property
        
        ''' <summary>
''' Gets or sets the editor zoom level as font size in points
''' </summary>
''' <value>Font size in points (6-72)</value>
''' <remarks>
''' Default is 11pt. Used by all text editors for consistent zoom level.
''' </remarks>
Public Property EditorZoomLevel As Integer
    Get
        Return GetValue("EditorZoomLevel", 11)
    End Get
    Set(Value As Integer)
        ' Clamp to valid range
        Dim lClampedValue As Integer = Math.Max(6, Math.Min(72, Value))
        SetValue("EditorZoomLevel", lClampedValue)
    End Set
End Property
'         
'         Public Property BottomPanelHeight As Integer
'             Get
'                 Return GetValue("BottomPanelHeight", 200)
'             End Get
'             Set(Value As Integer)
'                 SetValue("BottomPanelHeight", Value)
'             End Set
'         End Property
        
        Public Property ShowProjectExplorer As Boolean
            Get
                Return GetValue("ShowProjectExplorer", True)
            End Get
            Set(Value As Boolean)
                SetValue("ShowProjectExplorer", Value)
            End Set
        End Property
        
        Public Property ShowBottomPanel As Boolean
            Get
                Return GetValue("ShowBottomPanel", False)
            End Get
            Set(Value As Boolean)
                SetValue("ShowBottomPanel", Value)
            End Set
        End Property
        
        ' Editor Settings
        Public Property EditorFont As String
            Get
                Return GetValue("EditorFont", "Consolas 11")
            End Get
            Set(Value As String)
                SetValue("EditorFont", Value)
            End Set
        End Property
        
        Public Property TabWidth As Integer
            Get
                Return GetValue("TabWidth", 4)
            End Get
            Set(Value As Integer)
                SetValue("TabWidth", Value)
            End Set
        End Property
        
        Public Property UseTabs As Boolean
            Get
                Return GetValue("UseTabs", False)
            End Get
            Set(Value As Boolean)
                SetValue("UseTabs", Value)
            End Set
        End Property
        
        Public Property ShowLineNumbers As Boolean
            Get
                Return GetValue("ShowLineNumbers", True)
            End Get
            Set(Value As Boolean)
                SetValue("ShowLineNumbers", Value)
            End Set
        End Property
        
        Public Property WordWrap As Boolean
            Get
                Return GetValue("WordWrap", False)
            End Get
            Set(Value As Boolean)
                SetValue("WordWrap", Value)
            End Set
        End Property
        
        Public Property SyntaxHighlighting As Boolean
            Get
                Return GetValue("SyntaxHighlighting", True)
            End Get
            Set(Value As Boolean)
                SetValue("SyntaxHighlighting", Value)
            End Set
        End Property
        
        Public Property ShowNavigationDropdowns As Boolean
            Get
                Return GetValue("ShowNavigationDropdowns", True)
            End Get
            Set(Value As Boolean)
                SetValue("ShowNavigationDropdowns", Value)
            End Set
        End Property
        
        ' Theme Settings
        Public Property CurrentTheme As String
            Get
                Return GetValue("CurrentTheme", "Default Dark")
            End Get
            Set(Value As String)
                SetValue("CurrentTheme", Value)
            End Set
        End Property
        
        Public Property ColorTheme As String
            Get
                Return GetValue("ColorTheme", "Default Dark")
            End Get
            Set(Value As String)
                SetValue("ColorTheme", Value)
            End Set
        End Property
        
        ' Build Settings
        Public Property BuildConfiguration As String
            Get
                Return GetValue("BuildConfiguration", "Debug")
            End Get
            Set(Value As String)
                SetValue("BuildConfiguration", Value)
            End Set
        End Property
        
        Public Property BuildPlatform As String
            Get
                Return GetValue("BuildPlatform", "any CPU")
            End Get
            Set(Value As String)
                SetValue("BuildPlatform", Value)
            End Set
        End Property
        
        Public Property ShowBuildOutput As Boolean
            Get
                Return GetValue("ShowBuildOutput", True)
            End Get
            Set(Value As Boolean)
                SetValue("ShowBuildOutput", Value)
            End Set
        End Property
        
        ' AI Settings
        Public Property AIAssistantEnabled As Boolean
            Get
                Return GetValue("AIAssistantEnabled", False)
            End Get
            Set(Value As Boolean)
                SetValue("AIAssistantEnabled", Value)
            End Set
        End Property
        
        Public Property ClaudeAPIKey As String
            Get
                Return GetValue("ClaudeAPIKey", "")
            End Get
            Set(Value As String)
                SetValue("ClaudeAPIKey", Value)
            End Set
        End Property
        
        Public Property Mem0APIKey As String
            Get
                Return GetValue("Mem0APIKey", "")
            End Get
            Set(Value As String)
                SetValue("Mem0APIKey", Value)
            End Set
        End Property
        
        ' CodeSense Settings
        Public Property CodeSenseEnabled As Boolean
            Get
                Return GetValue("CodeSenseEnabled", True)
            End Get
            Set(Value As Boolean)
                SetValue("CodeSenseEnabled", Value)
            End Set
        End Property
        
        Public Property CodeSenseDelay As Integer
            Get
                Return GetValue("CodeSenseDelay", 300)
            End Get
            Set(Value As Integer)
                SetValue("CodeSenseDelay", Value)
            End Set
        End Property
        
        ' Find/Replace Settings
        Public Property FindCaseSensitive As Boolean
            Get
                Return GetValue("FindCaseSensitive", False)
            End Get
            Set(Value As Boolean)
                SetValue("FindCaseSensitive", Value)
            End Set
        End Property
        
        Public Property FindWholeWord As Boolean
            Get
                Return GetValue("FindWholeWord", False)
            End Get
            Set(Value As Boolean)
                SetValue("FindWholeWord", Value)
            End Set
        End Property
        
        Public Property FindUseRegex As Boolean
            Get
                Return GetValue("FindUseRegex", False)
            End Get
            Set(Value As Boolean)
                SetValue("FindUseRegex", Value)
            End Set
        End Property
        
        ' Git Settings
        Public Property GitUserName As String
            Get
                Return GetValue("GitUserName", "")
            End Get
            Set(Value As String)
                SetValue("GitUserName", Value)
            End Set
        End Property
        
        Public Property GitUserEmail As String
            Get
                Return GetValue("GitUserEmail", "")
            End Get
            Set(Value As String)
                SetValue("GitUserEmail", Value)
            End Set
        End Property
        
        ' Recent Files/Projects (stored as JSON arrays)
        Public Property RecentFiles As String
            Get
                Return GetValue("RecentFiles", "")
            End Get
            Set(Value As String)
                SetValue("RecentFiles", Value)
            End Set
        End Property
        
        Public Property RecentProjects As String
            Get
                Return GetValue("RecentProjects", "")
            End Get
            Set(Value As String)
                SetValue("RecentProjects", Value)
            End Set
        End Property
        
        ' Custom Settings (for extensibility)
        Public Property CustomSettings As String
            Get
                Return GetValue("CustomSettings", "")
            End Get
            Set(Value As String)
                SetValue("CustomSettings", Value)
            End Set
        End Property

        Public Property EnableLogging As Boolean
            Get
                Return GetValue("EnableLogging", False)
            End Get
            Set(Value As Boolean)
                SetValue("EnableLogging", Value)
            End Set
        End Property

        Public Property SaveWindowLayout As Boolean
            Get
                Return GetValue("SaveWindowLayout", True)
            End Get
            Set(Value As Boolean)
                SetValue("SaveWindowLayout", Value)
            End Set
        End Property

        Public Property AutoSave As Boolean
            Get
                Return GetValue("AutoSave", False)
            End Get
            Set(Value As Boolean)
                SetValue("AutoSave", Value)
            End Set
        End Property

        Public Property GitDefaultBranch As String
            Get
                Return GetValue("GitDefaultBranch", "")
            End Get
            Set(Value As String)
                SetValue("GitDefaultBranch", Value)
            End Set
        End Property

        Public Property GitEmail As String
            Get
                Return GetValue("GitEmail", "")
            End Get
            Set(Value As String)
                SetValue("GitEmail", Value)
            End Set
        End Property

        Public Property LastFilePath As String
            Get
                Return GetValue("LastFilePath", "")
            End Get
            Set(Value As String)
                SetValue("LastFilePath", Value)
            End Set
        End Property

        Public Property LastProjectPath As String
            Get
                Return GetValue("LastProjectPath", "")
            End Get
            Set(Value As String)
                SetValue("LastProjectPath", Value)
            End Set
        End Property

         Public Property ClearOutputOnBuild As Boolean
            Get
                Return GetValue("ClearOutputOnBuild", False)
            End Get
            Set(Value As Boolean)
                SetValue("ClearOutputOnBuild", Value)
            End Set
        End Property

        Public Property BuildBeforeRun As Boolean
            Get
                Return GetValue("BuildBeforeRun", True)
            End Get
            Set(Value As Boolean)
                SetValue("BuildBeforeRun", Value)
            End Set
        End Property

        Public Property ShowStatusBar As Boolean
            Get
                Return GetValue("ShowStatusBar", True)
            End Get
            Set(Value As Boolean)
                SetValue("ShowStatusBar", Value)
            End Set
        End Property

        Public Property AutoIndent As Boolean
            Get
                Return GetValue("AutoIndent", True)
            End Get
            Set(Value As Boolean)
                SetValue("AutoIndent", Value)
            End Set
        End Property

        Public Property HighlightCurrentLine As Boolean
            Get
                Return GetValue("HighlightCurrentLine", True)
            End Get
            Set(Value As Boolean)
                SetValue("HighlightCurrentLine", Value)
            End Set
        End Property

        Public Property BraceMatching As Boolean
            Get
                Return GetValue("BraceMatching", True)
            End Get
            Set(Value As Boolean)
                SetValue("BraceMatching", Value)
            End Set
        End Property

        Public Property ShowWhitespace As Boolean
            Get
                Return GetValue("ShowWhitespace", True)
            End Get
            Set(Value As Boolean)
                SetValue("ShowWhitespace", Value)
            End Set
        End Property

        Public Property ShowToolbar As Boolean
            Get
                Return GetValue("ShowToolbar", True)
            End Get
            Set(Value As Boolean)
                SetValue("ShowToolbar", Value)
            End Set
        End Property
        
        Public Property ToolbarShowLabels As Boolean
            Get
                Return GetValue("ToolbarShowLabels", True)
            End Get
            Set(Value As Boolean)
                SetValue("ToolbarShowLabels", Value)
            End Set
        End Property
        
        Public Property ToolbarLargeIcons As Boolean
            Get
                Return GetValue("ToolbarLargeIcons", True)
            End Get
            Set(Value As Boolean)
                SetValue("ToolbarLargeIcons", Value)
            End Set
        End Property
       
        ' ===== Persistence Methods =====
        
        Public Sub Save()
            SaveSettings()
        End Sub
        
        Public Sub Reload()
            LoadSettings()
        End Sub
        
        Private Sub LoadSettings()
            Try
                If File.Exists(pSettingsFilePath) Then
                    Dim lJsonContent As String = File.ReadAllText(pSettingsFilePath)
                    
                    If Not String.IsNullOrEmpty(lJsonContent) Then
                        pSettingsData = JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(lJsonContent)
                        If pSettingsData Is Nothing Then
                            pSettingsData = New Dictionary(Of String, Object)()
                        End If
                    End If
                End If
                
                pIsLoaded = True
                Console.WriteLine($"Settings loaded from: {pSettingsFilePath}")
                
            Catch ex As Exception
                Console.WriteLine($"LoadSettings error: {ex.Message}")
                pSettingsData = New Dictionary(Of String, Object)()
                pIsLoaded = True
            End Try
        End Sub
        
        Public Sub SaveSettings()
            Try
                Dim lJsonContent As String = JsonSerializer.Serialize(pSettingsData, New JsonSerializerOptions With {
                    .WriteIndented = True
                })
                
                File.WriteAllText(pSettingsFilePath, lJsonContent)
                Console.WriteLine($"Settings saved to: {pSettingsFilePath}")
                
            Catch ex As Exception
                Console.WriteLine($"SaveSettings error: {ex.Message}")
            End Try
        End Sub

        Public Sub ResetToDefaults()
            ' TODO: Implement ApplicationSettings.ResetToDefaults
        End Sub

        ''' <summary>
        ''' Gets or sets the startup tab for the left panel (0=Project, 1=Objects)
        ''' </summary>
        ''' <value>0 for Project tab, 1 for Objects tab</value>
        Public Property LeftPanelStartupTab As Integer
            Get
                Return GetValue("LeftPanelStartupTab", 0) ' Default to Project tab
            End Get
            Set(Value As Integer)
                ' Ensure value is either 0 or 1
                SetValue("LeftPanelStartupTab", Math.Max(0, Math.Min(1, Value)))
            End Set
        End Property
        
    End Class
    
End Namespace
