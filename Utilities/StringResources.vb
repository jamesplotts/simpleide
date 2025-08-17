' Utilities/StringResources.vb - Centralized string resource management
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Reflection
Imports System.Xml

' StringResources.vb
' Created: 2025-08-10 20:29:45

Namespace Utilities
    
    ''' <summary>
    ''' Manages all string resources for the application
    ''' Loads from embedded XML resource file
    ''' </summary>
    Public Class StringResources
        
        ' ===== Singleton Pattern =====
        Private Shared pInstance As StringResources
        Private Shared ReadOnly pLock As New Object()
        
        Public Shared ReadOnly Property Instance As StringResources
            Get
                If pInstance Is Nothing Then
                    SyncLock pLock
                        If pInstance Is Nothing Then
                            pInstance = New StringResources()
                        End If
                    End SyncLock
                End If
                Return pInstance
            End Get
        End Property
        
        ' ===== Private Fields =====
        Private pStringCache As New Dictionary(Of String, String)()
        Private pIsLoaded As Boolean = False
        
        ' ===== String Resource Keys =====
        ' Project Templates
        Public Const KEY_CONSOLE_PROJECT_TEMPLATE As String = "ConsoleProjectTemplate"
        Public Const KEY_LIBRARY_PROJECT_TEMPLATE As String = "LibraryProjectTemplate"
        Public Const KEY_WINFORMS_PROJECT_TEMPLATE As String = "WinFormsProjectTemplate"
        Public Const KEY_GTK_PROJECT_TEMPLATE As String = "GtkProjectTemplate"
        Public Const KEY_ASSEMBLYINFO_TEMPLATE As String = "AssemblyInfoTemplate"
        Public Const KEY_VBPROJ_TEMPLATE As String = "VbProjTemplate"
        Public Const KEY_RESX_TEMPLATE As String = "ResxTemplate"
        
        ' Code Templates
        Public Const KEY_CLASS_TEMPLATE As String = "ClassTemplate"
        Public Const KEY_MODULE_TEMPLATE As String = "ModuleTemplate"
        Public Const KEY_INTERFACE_TEMPLATE As String = "InterfaceTemplate"
        Public Const KEY_STRUCTURE_TEMPLATE As String = "StructureTemplate"
        Public Const KEY_ENUM_TEMPLATE As String = "EnumTemplate"
        Public Const KEY_DELEGATE_TEMPLATE As String = "DelegateTemplate"
        
        ' UI Strings
        Public Const KEY_ABOUT_TEXT As String = "AboutText"
        Public Const KEY_WELCOME_MESSAGE As String = "WelcomeMessage"
        
        ' Error Messages
        Public Const KEY_ERROR_PROJECT_NOT_FOUND As String = "ErrorProjectNotFound"
        Public Const KEY_ERROR_FILE_NOT_FOUND As String = "ErrorFileNotFound"
        Public Const KEY_ERROR_BUILD_FAILED As String = "ErrorBuildFailed"
        Public Const KEY_ERROR_SAVE_FAILED As String = "ErrorSaveFailed"
        Public Const KEY_ERROR_INVALID_PROJECT As String = "ErrorInvalidProject"
        Public Const KEY_ERROR_NO_EDITOR As String = "ErrorNoEditor"
        Public Const KEY_ERROR_PARSING_FAILED As String = "ErrorParsingFailed"
        
        ' Info Messages
        Public Const KEY_INFO_PROJECT_LOADED As String = "InfoProjectLoaded"
        Public Const KEY_INFO_BUILD_SUCCEEDED As String = "InfoBuildSucceeded"
        Public Const KEY_INFO_FILE_SAVED As String = "InfoFileSaved"
        Public Const KEY_INFO_ALL_FILES_SAVED As String = "InfoAllFilesSaved"
        Public Const KEY_INFO_PROJECT_CREATED As String = "InfoProjectCreated"
        
        ' Dialog Messages
        Public Const KEY_DIALOG_SAVE_CHANGES As String = "DialogSaveChanges"
        Public Const KEY_DIALOG_OVERWRITE_FILE As String = "DialogOverwriteFile"
        Public Const KEY_DIALOG_DELETE_FILE As String = "DialogDeleteFile"
        Public Const KEY_DIALOG_CLOSE_PROJECT As String = "DialogCloseProject"
        
        ' Status Messages
        Public Const KEY_STATUS_READY As String = "StatusReady"
        Public Const KEY_STATUS_BUILDING As String = "StatusBuilding"
        Public Const KEY_STATUS_SAVING As String = "StatusSaving"
        Public Const KEY_STATUS_LOADING As String = "StatusLoading"
        Public Const KEY_STATUS_PARSING As String = "StatusParsing"
        
        ' Other Resources
        Public Const KEY_GTK_PACKAGE_REFERENCE As String = "GtkPackageReference"
        Public Const KEY_GITIGNORE_TEMPLATE As String = "GitIgnoreTemplate"
        
        ' ===== Constructor =====
        Private Sub New()
            LoadResources()
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Get a string resource by key
        ''' </summary>
        Public Function GetString(vKey As String) As String
            Try
                If Not pIsLoaded Then
                    LoadResources()
                End If
                
                If pStringCache.ContainsKey(vKey) Then
                    Return pStringCache(vKey)
                End If
                
                Console.WriteLine($"StringResources: Key not found: {vKey}")
                Return $"[{vKey}]" ' Return key in brackets if not found
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.GetString error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Get a template with parameter substitution
        ''' </summary>
        Public Function GetTemplate(vKey As String, vParameters As Dictionary(Of String, String)) As String
            Try
                Dim lTemplate As String = GetString(vKey)
                
                If String.IsNullOrEmpty(lTemplate) OrElse lTemplate.StartsWith("[") Then
                    Return ""
                End If
                
                ' Replace parameters in template
                For Each lParam In vParameters
                    lTemplate = lTemplate.Replace($"{{{lParam.Key}}}", lParam.Value)
                Next
                
                Return lTemplate
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.GetTemplate error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Format a message with parameters
        ''' </summary>
        Public Function FormatMessage(vKey As String, ParamArray vArgs() As Object) As String
            Try
                Dim lMessage As String = GetString(vKey)
                
                If String.IsNullOrEmpty(lMessage) OrElse lMessage.StartsWith("[") Then
                    Return ""
                End If
                
                ' Simple parameter replacement for {0}, {1}, etc.
                For i As Integer = 0 To vArgs.Length - 1
                    lMessage = lMessage.Replace($"{{{i}}}", vArgs(i)?.ToString())
                Next
                
                ' Also support named parameters
                If vArgs.Length = 1 AndAlso TypeOf vArgs(0) Is Dictionary(Of String, String) Then
                    Dim lParams As Dictionary(Of String, String) = DirectCast(vArgs(0), Dictionary(Of String, String))
                    For Each lParam In lParams
                        lMessage = lMessage.Replace($"{{{lParam.Key}}}", lParam.Value)
                    Next
                End If
                
                Return lMessage
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.FormatMessage error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Check if a resource key exists
        ''' </summary>
        Public Function HasKey(vKey As String) As Boolean
            Try
                If Not pIsLoaded Then
                    LoadResources()
                End If
                
                Return pStringCache.ContainsKey(vKey)
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.HasKey error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Reload resources from files
        ''' </summary>
        Public Sub ReloadResources()
            Try
                pStringCache.Clear()
                pIsLoaded = False
                LoadResources()
                
                Console.WriteLine($"StringResources reloaded: {pStringCache.Count} strings")
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.ReloadResources error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Load all string resources from embedded XML
        ''' </summary>
        Private Sub LoadResources()
            Try
                ' Load from embedded resource first
                LoadEmbeddedResources()
                
                ' Then try to load external overrides
                LoadExternalResources()
                
                pIsLoaded = True
                
                Console.WriteLine($"StringResources loaded: {pStringCache.Count} strings")
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.LoadResources error: {ex.Message}")
                ' Even on error, mark as loaded to prevent infinite recursion
                pIsLoaded = True
            End Try
        End Sub
        
        ''' <summary>
        ''' Load from embedded Strings.xml resource
        ''' </summary>
        Private Sub LoadEmbeddedResources()
            Try
                Dim lAssembly As Assembly = Assembly.GetExecutingAssembly()
                
                ' Try different possible resource names
                Dim lResourceNames() As String = {
                    "SimpleIDE.Resources.Strings.xml",
                    "SimpleIDE.Strings.xml",
                    "Resources.Strings.xml",
                    "Strings.xml"
                }
                
                Dim lResourceStream As Stream = Nothing
                Dim lFoundName As String = ""
                
                ' Try each possible name
                For Each lName In lResourceNames
                    lResourceStream = lAssembly.GetManifestResourceStream(lName)
                    If lResourceStream IsNot Nothing Then
                        lFoundName = lName
                        Exit For
                    End If
                Next
                
                ' If not found by name, list all resources (for debugging)
                If lResourceStream Is Nothing Then
                    Dim lAllResources() As String = lAssembly.GetManifestResourceNames()
                    Console.WriteLine("Available embedded resources:")
                    For Each lResource In lAllResources
                        Console.WriteLine($"  - {lResource}")
                        If lResource.Contains("Strings.xml") Then
                            lResourceStream = lAssembly.GetManifestResourceStream(lResource)
                            lFoundName = lResource
                            Exit For
                        End If
                    Next
                End If
                
                If lResourceStream IsNot Nothing Then
                    Console.WriteLine($"Loading embedded resource: {lFoundName}")
                    Using lResourceStream
                        LoadXmlFromStream(lResourceStream)
                    End Using
                Else
                    Console.WriteLine("Warning: Strings.xml not found in embedded resources")
                    ' Load minimal fallback strings
                    LoadFallbackStrings()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.LoadEmbeddedResources error: {ex.Message}")
                LoadFallbackStrings()
            End Try
        End Sub
        
        ''' <summary>
        ''' Load XML resources from a stream
        ''' </summary>
        Private Sub LoadXmlFromStream(vStream As Stream)
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(vStream)
                
                Dim lStringNodes As XmlNodeList = lDoc.SelectNodes("//Resources/String")
                
                For Each lNode As XmlNode In lStringNodes
                    Dim lKey As String = lNode.Attributes("key")?.Value
                    Dim lValue As String = lNode.InnerText.Trim()
                    
                    If Not String.IsNullOrEmpty(lKey) Then
                        pStringCache(lKey) = lValue
                    End If
                Next
                
                Console.WriteLine($"Loaded {lStringNodes.Count} strings from XML")
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.LoadXmlFromStream error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Load external resource files if they exist
        ''' </summary>
        Private Sub LoadExternalResources()
            Try
                ' Look for external resource files in the application directory
                Dim lAppDir As String = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                Dim lResourceFile As String = System.IO.Path.Combine(lAppDir, "Resources", "Strings.xml")
                
                If File.Exists(lResourceFile) Then
                    Console.WriteLine($"Loading external resources: {lResourceFile}")
                    LoadXmlFromFile(lResourceFile)
                End If
                
                ' Also check for user overrides
                Dim lUserResourceFile As String = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SimpleIDE",
                    "Resources",
                    "Strings.xml"
                )
                
                If File.Exists(lUserResourceFile) Then
                    Console.WriteLine($"Loading user resources: {lUserResourceFile}")
                    LoadXmlFromFile(lUserResourceFile)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.LoadExternalResources error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Load resources from XML file
        ''' </summary>
        Private Sub LoadXmlFromFile(vFilePath As String)
            Try
                Using lStream As New FileStream(vFilePath, FileMode.Open, FileAccess.Read)
                    LoadXmlFromStream(lStream)
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.LoadXmlFromFile error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Load minimal fallback strings if resource file not found
        ''' </summary>
        Private Sub LoadFallbackStrings()
            Try
                ' Provide minimal templates so the app can still function
                pStringCache(KEY_STATUS_READY) = "Ready"
                pStringCache(KEY_STATUS_BUILDING) = "Building..."
                pStringCache(KEY_STATUS_SAVING) = "Saving..."
                pStringCache(KEY_STATUS_LOADING) = "Loading..."
                pStringCache(KEY_ERROR_FILE_NOT_FOUND) = "File not found: {FilePath}"
                pStringCache(KEY_INFO_FILE_SAVED) = "File saved: {FileName}"
                
                Console.WriteLine("Loaded fallback strings")
                
            Catch ex As Exception
                Console.WriteLine($"StringResources.LoadFallbackStrings error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
