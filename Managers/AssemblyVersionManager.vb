' Models/AssemblyVersionManager.vb - Manages assembly version auto-incrementing
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml
Imports SimpleIDE.Utilities

Namespace Managers
    Public Class AssemblyVersionManager
        
        Private pProjectFile As String
        
        Public Sub New(vProjectFile As String)
            pProjectFile = vProjectFile
        End Sub
        
        ' Increment build number if auto-increment is enabled
        Public Function IncrementBuildNumberIfEnabled() As Boolean
            Try
                ' Check if auto-increment is enabled in project file
                If Not IsAutoIncrementEnabled() Then
                    Return False
                End If
                
                ' Find AssemblyInfo.vb
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                
                If Not File.Exists(lAssemblyInfoPath) Then
                    Console.WriteLine("AssemblyInfo.vb not found - creating New file")
                    CreateAssemblyInfoFile(lAssemblyInfoPath)
                End If
                
                ' Read content
                Dim lContent As String = File.ReadAllText(lAssemblyInfoPath)
                Dim lModified As Boolean = False
                
                ' Update AssemblyVersion
                lContent = IncrementVersionInContent(lContent, "AssemblyVersion", lModified)
                
                ' Update AssemblyFileVersion
                lContent = IncrementVersionInContent(lContent, "AssemblyFileVersion", lModified)
                
                ' Save if modified
                If lModified Then
                    File.WriteAllText(lAssemblyInfoPath, lContent)
                    Console.WriteLine("Assembly version incremented successfully")
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IncrementBuildNumberIfEnabled error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Check if auto-increment is enabled in project settings
        Private Function IsAutoIncrementEnabled() As Boolean
            Try
                ' TODO: This should check project file or settings for auto-increment flag
                ' For now, return False - this feature can be implemented later
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsAutoIncrementEnabled error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get AssemblyInfo.vb path
        Private Function GetAssemblyInfoPath() As String
            Try
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                Dim lMyProjectDir As String = System.IO.Path.Combine(lProjectDir, "My project")
                Return System.IO.Path.Combine(lMyProjectDir, "AssemblyInfo.vb")
                
            Catch ex As Exception
                Console.WriteLine($"GetAssemblyInfoPath error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' Create AssemblyInfo file if it doesn't exist
        Private Sub CreateAssemblyInfoFile(vPath As String)
            Try
                ' Create directory if needed
                Dim lDir As String = System.IO.Path.GetDirectoryName(vPath)
                If Not Directory.Exists(lDir) Then
                    Directory.CreateDirectory(lDir)
                End If
                
                ' Get project name
                Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pProjectFile)
                
                ' FIXED: Create the missing lParams dictionary
                Dim lParams As New Dictionary(Of String, String) From {
                    {"ProjectName", lProjectName},
                    {"Description", $"{lProjectName} application"},
                    {"Company", ""},
                    {"Year", DateTime.Now.Year.ToString()}
                }

                Dim lContent As String = StringResources.Instance.GetTemplate(StringResources.KEY_ASSEMBLYINFO_TEMPLATE, lParams)
                
                ' Write the file
                File.WriteAllText(vPath, lContent)
                Console.WriteLine($"Created AssemblyInfo.vb: {vPath}")
                
            Catch ex As Exception
                Console.WriteLine($"CreateAssemblyInfoFile error: {ex.Message}")
            End Try
        End Sub
        
        ' Increment version in content
        Private Function IncrementVersionInContent(vContent As String, vAttributeName As String, ByRef vModified As Boolean) As String
            Try
                ' Pattern to match version attributes
                Dim lPattern As String = $"<Assembly:\s*{vAttributeName}\s*\(\s*""(\d+)\.(\d+)\.(\d+)\.(\d+)""\s*\)>"
                Dim lMatch As Match = Regex.Match(vContent, lPattern, RegexOptions.IgnoreCase)
                
                If lMatch.Success Then
                    ' Extract version components
                    Dim lMajor As Integer = Integer.Parse(lMatch.Groups(1).Value)
                    Dim lMinor As Integer = Integer.Parse(lMatch.Groups(2).Value)
                    Dim lBuildNumber As Integer = Integer.Parse(lMatch.Groups(3).Value)
                    Dim lRevision As Integer = Integer.Parse(lMatch.Groups(4).Value)
                    
                    ' Increment build number
                    lBuildNumber += 1
                    
                    ' Create new version string
                    Dim lNewVersion As String = $"<Assembly: {vAttributeName}(""{lMajor}.{lMinor}.{lBuildNumber}.{lRevision}"")>"
                    
                    ' Replace in content
                    vContent = vContent.Replace(lMatch.Value, lNewVersion)
                    vModified = True
                    
                    Console.WriteLine($"{vAttributeName} incremented to {lMajor}.{lMinor}.{lBuildNumber}.{lRevision}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error incrementing {vAttributeName}: {ex.Message}")
            End Try
            
            Return vContent
        End Function
        
        ' Get current version from AssemblyInfo
        Public Function GetCurrentVersion() As Version
            Try
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                
                If File.Exists(lAssemblyInfoPath) Then
                    Dim lContent As String = File.ReadAllText(lAssemblyInfoPath)
                    
                    ' Extract AssemblyVersion
                    Dim lPattern As String = "<Assembly:\s*AssemblyVersion\s*\(""(\d+)\.(\d+)\.(\d+)\.(\d+)""\)>"
                    Dim lMatch As Match = Regex.Match(lContent, lPattern, RegexOptions.IgnoreCase)
                    
                    If lMatch.Success Then
                        Dim lMajor As Integer = Integer.Parse(lMatch.Groups(1).Value)
                        Dim lMinor As Integer = Integer.Parse(lMatch.Groups(2).Value)
                        Dim lBuild As Integer = Integer.Parse(lMatch.Groups(3).Value)
                        Dim lRevision As Integer = Integer.Parse(lMatch.Groups(4).Value)
                        
                        Return New Version(lMajor, lMinor, lBuild, lRevision)
                    End If
                End If
                
                ' Default version
                Return New Version(1, 0, 0, 0)
                
            Catch ex As Exception
                Console.WriteLine($"GetCurrentVersion error: {ex.Message}")
                Return New Version(1, 0, 0, 0)
            End Try
        End Function

        Private pManifestPath As String = String.Empty
        Private pIsManifestEmbeddingEnabled As Boolean = False
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets whether manifest embedding is enabled
        ''' </summary>
        Public ReadOnly Property IsManifestEmbeddingEnabled As Boolean
            Get
                Return pIsManifestEmbeddingEnabled
            End Get
        End Property
        
        ' ===== Methods =====
        
        ''' <summary>
        ''' Set the path to the manifest file
        ''' </summary>
        Public Sub SetManifestPath(vPath As String)
            Try
                If Not String.IsNullOrEmpty(vPath) AndAlso File.Exists(vPath) Then
                    pManifestPath = vPath
                    pIsManifestEmbeddingEnabled = True
                Else
                    pManifestPath = String.Empty
                    pIsManifestEmbeddingEnabled = False
                End If
            Catch ex As Exception
                Console.WriteLine($"SetManifestPath error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get the current manifest path
        ''' </summary>
        Public Function GetManifestPath() As String
            Return pManifestPath
        End Function
        
        ''' <summary>
        ''' Enable or disable manifest embedding
        ''' </summary>
        Public Sub SetManifestEmbeddingEnabled(vEnabled As Boolean)
            pIsManifestEmbeddingEnabled = vEnabled
            If Not vEnabled Then
                pManifestPath = String.Empty
            End If
        End Sub
        
    End Class
End Namespace
