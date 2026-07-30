' Replace: SimpleIDE.Managers.AssemblyVersionManager
' Models/AssemblyVersionManager.vb - Manages assembly version auto-incrementing for both SDK-style and classic projects
Imports System.IO
Imports System.Text
Imports System.Xml
Imports SimpleIDE.Utilities

Namespace Managers
    
    ''' <summary>
    ''' Manages assembly version auto-incrementing for both modern SDK-style and classic projects
    ''' </summary>
    ''' <remarks>
    ''' Supports both .vbproj-based versioning (modern .NET) and AssemblyInfo.vb (classic)
    ''' </remarks>
    Public Class AssemblyVersionManager
        
        Private pProjectFile As String
        Private pIsSdkStyleProject As Boolean
        Private pUsesAssemblyInfo As Boolean
        
        ''' <summary>
        ''' Initializes a new instance of the AssemblyVersionManager class
        ''' </summary>
        ''' <param name="vProjectFile">Path to the .vbproj file</param>
        Public Sub New(vProjectFile As String)
            pProjectFile = vProjectFile
            DetectProjectStyle()
        End Sub
        
        ''' <summary>
        ''' Gets whether this is an SDK-style project
        ''' </summary>
        Public ReadOnly Property IsSdkStyleProject As Boolean
            Get
                Return pIsSdkStyleProject
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether the project uses AssemblyInfo.vb for versioning
        ''' </summary>
        Public ReadOnly Property UsesAssemblyInfo As Boolean
            Get
                Return pUsesAssemblyInfo
            End Get
        End Property
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Increment build number if auto-increment is enabled
        ''' </summary>
        ''' <returns>True if version was incremented, False otherwise</returns>
        Public Function IncrementBuildNumberIfEnabled() As Boolean
            Try
                ' Check if auto-increment is enabled in project file
                If Not IsAutoIncrementEnabled() Then
                    Return False
                End If
                
                ' Determine versioning approach
                If Not pUsesAssemblyInfo Then
                    ' Use .vbproj versioning for SDK-style projects or projects without AssemblyInfo
                    Return IncrementVersionInProject()
                Else
                    ' Use traditional AssemblyInfo.vb approach
                    Return IncrementVersionInAssemblyInfo()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IncrementBuildNumberIfEnabled error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Get current version from project or AssemblyInfo
        ''' </summary>
        ''' <returns>Current version or 1.0.0.0 if not found</returns>
        Public Function GetCurrentVersion() As Version
            Try
                If Not pUsesAssemblyInfo Then
                    Return GetVersionFromProject()
                Else
                    Return GetVersionFromAssemblyInfo()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetCurrentVersion error: {ex.Message}")
                Return New Version(1, 0, 0, 0)
            End Try
        End Function
        
        ''' <summary>
        ''' Set a specific version number
        ''' </summary>
        ''' <param name="vVersion">Version to set</param>
        ''' <returns>True if successful, False otherwise</returns>
        Public Function SetVersion(vVersion As Version) As Boolean
            Try
                If Not pUsesAssemblyInfo Then
                    Return SetVersionInProject(vVersion)
                Else
                    Return SetVersionInAssemblyInfo(vVersion)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetVersion error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Private Project Detection Methods =====
        
        ''' <summary>
        ''' Detects whether this is an SDK-style project and how it handles versioning
        ''' </summary>
        Private Sub DetectProjectStyle()
            Try
                If Not File.Exists(pProjectFile) Then
                    pIsSdkStyleProject = False
                    pUsesAssemblyInfo = True
                    Return
                End If
                
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Check for SDK attribute (modern SDK-style projects)
                Dim lRootElement As XmlElement = lDoc.DocumentElement
                pIsSdkStyleProject = lRootElement IsNot Nothing AndAlso lRootElement.HasAttribute("Sdk")
                
                ' Check if AssemblyInfo.vb exists
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                Dim lAssemblyInfoExists As Boolean = File.Exists(lAssemblyInfoPath)
                
                If pIsSdkStyleProject Then
                    ' For SDK-style projects, check if GenerateAssemblyInfo is disabled
                    Dim lGenerateNode As XmlNode = lDoc.SelectSingleNode("//PropertyGroup/GenerateAssemblyInfo")
                    If lGenerateNode IsNot Nothing AndAlso 
                       lGenerateNode.InnerText.Equals("false", StringComparison.OrdinalIgnoreCase) Then
                        ' GenerateAssemblyInfo is explicitly disabled, use AssemblyInfo.vb if it exists
                        pUsesAssemblyInfo = lAssemblyInfoExists
                    Else
                        ' Use .vbproj versioning by default for SDK projects
                        pUsesAssemblyInfo = False
                    End If
                Else
                    ' Classic project - use AssemblyInfo.vb if it exists
                    pUsesAssemblyInfo = lAssemblyInfoExists
                End If
                
                Console.WriteLine($"Project style: {If(pIsSdkStyleProject, "SDK-style", "Classic")}, " &
                                $"Uses AssemblyInfo: {pUsesAssemblyInfo}")
                
            Catch ex As Exception
                Console.WriteLine($"DetectProjectStyle error: {ex.Message}")
                ' Default to classic with AssemblyInfo
                pIsSdkStyleProject = False
                pUsesAssemblyInfo = True
            End Try
        End Sub
        
        ' ===== Private .vbproj Versioning Methods =====
        
        ''' <summary>
        ''' Increment version directly in .vbproj file
        ''' </summary>
        Private Function IncrementVersionInProject() As Boolean
            Try
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(pProjectFile)
                
                Dim lModified As Boolean = False
                
                ' Handle different version properties
                lModified = IncrementProjectProperty(lDoc, "Version") Or lModified
                lModified = IncrementProjectProperty(lDoc, "AssemblyVersion") Or lModified
                lModified = IncrementProjectProperty(lDoc, "FileVersion") Or lModified
                
                ' If no version properties exist, create them
                If Not lModified Then
                    lModified = CreateVersionProperties(lDoc, New Version(1, 0, 1, 0))
                End If
                
                If lModified Then
                    lDoc.Save(pProjectFile)
                    Console.WriteLine("Version incremented in .vbproj")
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IncrementVersionInProject error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Increment a specific version property in the project file
        ''' </summary>
        Private Function IncrementProjectProperty(vDoc As XmlDocument, vPropertyName As String) As Boolean
            Try
                Dim lNode As XmlNode = vDoc.SelectSingleNode($"//PropertyGroup/{vPropertyName}")
                If lNode Is Nothing Then Return False
                
                Dim lVersionText As String = lNode.InnerText.Trim()
                Dim lVersion As Version = Nothing
                
                ' Parse version (handle both 3-part and 4-part versions)
                If Version.TryParse(lVersionText, lVersion) Then
                    ' Increment build number
                    Dim lNewVersion As New Version(
                        lVersion.Major, 
                        lVersion.Minor, 
                        Math.Max(0, lVersion.Build) + 1,
                        Math.Max(0, lVersion.Revision))
                    
                    ' Update node
                    If vPropertyName = "Version" AndAlso lVersion.Revision = -1 Then
                        ' For Version property, use 3-part format if original was 3-part
                        lNode.InnerText = $"{lNewVersion.Major}.{lNewVersion.Minor}.{lNewVersion.Build}"
                    Else
                        lNode.InnerText = lNewVersion.ToString()
                    End If
                    
                    Console.WriteLine($"{vPropertyName} incremented to {lNode.InnerText}")
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IncrementProjectProperty error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Create version properties if they don't exist
        ''' </summary>
        Private Function CreateVersionProperties(vDoc As XmlDocument, vVersion As Version) As Boolean
            Try
                ' Find or create first PropertyGroup
                Dim lPropertyGroup As XmlNode = vDoc.SelectSingleNode("//PropertyGroup")
                If lPropertyGroup Is Nothing Then
                    ' Create new PropertyGroup
                    lPropertyGroup = vDoc.CreateElement("PropertyGroup")
                    vDoc.DocumentElement.AppendChild(lPropertyGroup)
                End If
                
                ' Add Version property if not exists
                If vDoc.SelectSingleNode("//PropertyGroup/Version") Is Nothing Then
                    Dim lVersionNode As XmlElement = vDoc.CreateElement("Version")
                    lVersionNode.InnerText = $"{vVersion.Major}.{vVersion.Minor}.{vVersion.Build}"
                    lPropertyGroup.AppendChild(lVersionNode)
                    Console.WriteLine($"Created Version property: {lVersionNode.InnerText}")
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"CreateVersionProperties error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Get version from project file
        ''' </summary>
        Private Function GetVersionFromProject() As Version
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Try different version properties in order of preference
                Dim lVersionProperties() As String = {"AssemblyVersion", "Version", "FileVersion"}
                
                For Each lProperty In lVersionProperties
                    Dim lNode As XmlNode = lDoc.SelectSingleNode($"//PropertyGroup/{lProperty}")
                    If lNode IsNot Nothing Then
                        Dim lVersion As Version = Nothing
                        If Version.TryParse(lNode.InnerText.Trim(), lVersion) Then
                            Return lVersion
                        End If
                    End If
                Next
                
                ' Default version
                Return New Version(1, 0, 0, 0)
                
            Catch ex As Exception
                Console.WriteLine($"GetVersionFromProject error: {ex.Message}")
                Return New Version(1, 0, 0, 0)
            End Try
        End Function
        
        ''' <summary>
        ''' Set version in project file
        ''' </summary>
        Private Function SetVersionInProject(vVersion As Version) As Boolean
            Try
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(pProjectFile)
                
                ' Update or create version properties
                SetProjectProperty(lDoc, "Version", $"{vVersion.Major}.{vVersion.Minor}.{vVersion.Build}")
                SetProjectProperty(lDoc, "AssemblyVersion", vVersion.ToString())
                SetProjectProperty(lDoc, "FileVersion", vVersion.ToString())
                
                lDoc.Save(pProjectFile)
                Console.WriteLine($"Version set to {vVersion} in .vbproj")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SetVersionInProject error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Set or create a project property
        ''' </summary>
        Private Sub SetProjectProperty(vDoc As XmlDocument, vPropertyName As String, vValue As String)
            Try
                Dim lNode As XmlNode = vDoc.SelectSingleNode($"//PropertyGroup/{vPropertyName}")
                
                If lNode IsNot Nothing Then
                    lNode.InnerText = vValue
                Else
                    ' Create property
                    Dim lPropertyGroup As XmlNode = vDoc.SelectSingleNode("//PropertyGroup")
                    If lPropertyGroup IsNot Nothing Then
                        Dim lNewNode As XmlElement = vDoc.CreateElement(vPropertyName)
                        lNewNode.InnerText = vValue
                        lPropertyGroup.AppendChild(lNewNode)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetProjectProperty error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private AssemblyInfo.vb Methods =====
        
        ''' <summary>
        ''' Increment version in AssemblyInfo.vb file
        ''' </summary>
        Private Function IncrementVersionInAssemblyInfo() As Boolean
            Try
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                
                If Not File.Exists(lAssemblyInfoPath) Then
                    ' For projects that should use AssemblyInfo but don't have it,
                    ' fall back to .vbproj versioning
                    Console.WriteLine("AssemblyInfo.vb not found - using .vbproj versioning")
                    pUsesAssemblyInfo = False
                    Return IncrementVersionInProject()
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
                    Console.WriteLine("Version incremented in AssemblyInfo.vb")
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IncrementVersionInAssemblyInfo error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Increment version in AssemblyInfo content
        ''' </summary>
        Private Function IncrementVersionInContent(vContent As String, vAttributeName As String, 
                                                  ByRef vModified As Boolean) As String
            Try
                ' Pattern to match version attributes - NO REGEX per requirements
                Dim lSearchPattern As String = $"<Assembly: {vAttributeName}("""
                Dim lStartIndex As Integer = vContent.IndexOf(lSearchPattern)
                
                If lStartIndex >= 0 Then
                    ' Find the version string
                    Dim lVersionStart As Integer = lStartIndex + lSearchPattern.Length
                    Dim lVersionEnd As Integer = vContent.IndexOf("""", lVersionStart)
                    
                    If lVersionEnd > lVersionStart Then
                        Dim lVersionText As String = vContent.Substring(lVersionStart, lVersionEnd - lVersionStart)
                        Dim lVersion As Version = Nothing
                        
                        If Version.TryParse(lVersionText, lVersion) Then
                            ' Increment build number
                            Dim lNewVersion As New Version(
                                lVersion.Major,
                                lVersion.Minor,
                                Math.Max(0, lVersion.Build) + 1,
                                Math.Max(0, lVersion.Revision))
                            
                            ' Replace old version with new
                            Dim lOldAttribute As String = vContent.Substring(lStartIndex, 
                                                                            lVersionEnd - lStartIndex + 2) ' +2 for ">
                            Dim lNewAttribute As String = $"<Assembly: {vAttributeName}(""{lNewVersion}"")>"
                            
                            vContent = vContent.Replace(lOldAttribute, lNewAttribute)
                            vModified = True
                            
                            Console.WriteLine($"{vAttributeName} incremented to {lNewVersion}")
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IncrementVersionInContent error: {ex.Message}")
            End Try
            
            Return vContent
        End Function
        
        ''' <summary>
        ''' Get version from AssemblyInfo.vb
        ''' </summary>
        Private Function GetVersionFromAssemblyInfo() As Version
            Try
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                
                If Not File.Exists(lAssemblyInfoPath) Then
                    ' Fall back to project version
                    Return GetVersionFromProject()
                End If
                
                Dim lContent As String = File.ReadAllText(lAssemblyInfoPath)
                
                ' Find AssemblyVersion attribute - NO REGEX
                Dim lSearchPattern As String = "<Assembly: AssemblyVersion("""
                Dim lStartIndex As Integer = lContent.IndexOf(lSearchPattern)
                
                If lStartIndex >= 0 Then
                    Dim lVersionStart As Integer = lStartIndex + lSearchPattern.Length
                    Dim lVersionEnd As Integer = lContent.IndexOf("""", lVersionStart)
                    
                    If lVersionEnd > lVersionStart Then
                        Dim lVersionText As String = lContent.Substring(lVersionStart, lVersionEnd - lVersionStart)
                        Dim lVersion As Version = Nothing
                        
                        If Version.TryParse(lVersionText, lVersion) Then
                            Return lVersion
                        End If
                    End If
                End If
                
                Return New Version(1, 0, 0, 0)
                
            Catch ex As Exception
                Console.WriteLine($"GetVersionFromAssemblyInfo error: {ex.Message}")
                Return New Version(1, 0, 0, 0)
            End Try
        End Function
        
        ''' <summary>
        ''' Set version in AssemblyInfo.vb
        ''' </summary>
        Private Function SetVersionInAssemblyInfo(vVersion As Version) As Boolean
            Try
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                
                If Not File.Exists(lAssemblyInfoPath) Then
                    ' Fall back to project versioning
                    Return SetVersionInProject(vVersion)
                End If
                
                Dim lContent As String = File.ReadAllText(lAssemblyInfoPath)
                Dim lModified As Boolean = False
                
                ' Update AssemblyVersion
                lContent = SetVersionInContent(lContent, "AssemblyVersion", vVersion, lModified)
                
                ' Update AssemblyFileVersion
                lContent = SetVersionInContent(lContent, "AssemblyFileVersion", vVersion, lModified)
                
                If lModified Then
                    File.WriteAllText(lAssemblyInfoPath, lContent)
                    Console.WriteLine($"Version set to {vVersion} in AssemblyInfo.vb")
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"SetVersionInAssemblyInfo error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Set version in AssemblyInfo content
        ''' </summary>
        Private Function SetVersionInContent(vContent As String, vAttributeName As String, 
                                            vVersion As Version, ByRef vModified As Boolean) As String
            Try
                ' Pattern to find version attributes - NO REGEX
                Dim lSearchPattern As String = $"<Assembly: {vAttributeName}("""
                Dim lStartIndex As Integer = vContent.IndexOf(lSearchPattern)
                
                If lStartIndex >= 0 Then
                    ' Find and replace existing
                    Dim lVersionEnd As Integer = vContent.IndexOf(""")", lStartIndex + lSearchPattern.Length)
                    If lVersionEnd > 0 Then
                        Dim lOldAttribute As String = vContent.Substring(lStartIndex, 
                                                                        lVersionEnd - lStartIndex + 2)
                        Dim lNewAttribute As String = $"<Assembly: {vAttributeName}(""{vVersion}"")>"
                        
                        vContent = vContent.Replace(lOldAttribute, lNewAttribute)
                        vModified = True
                    End If
                Else
                    ' Add new attribute before last line or at end
                    Dim lNewAttribute As String = $"<Assembly: {vAttributeName}(""{vVersion}"")>"
                    vContent = vContent.TrimEnd() & Environment.NewLine & lNewAttribute
                    vModified = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetVersionInContent error: {ex.Message}")
            End Try
            
            Return vContent
        End Function
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Get AssemblyInfo.vb path
        ''' </summary>
        Private Function GetAssemblyInfoPath() As String
            Try
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                Dim lMyProjectDir As String = System.IO.Path.Combine(lProjectDir, "My Project")
                Return System.IO.Path.Combine(lMyProjectDir, "AssemblyInfo.vb")
                
            Catch ex As Exception
                Console.WriteLine($"GetAssemblyInfoPath error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Check if auto-increment is enabled in project settings
        ''' </summary>
        Private Function IsAutoIncrementEnabled() As Boolean
            Try
                ' Check for AutoIncrementVersion property in project file
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                Dim lNode As XmlNode = lDoc.SelectSingleNode("//PropertyGroup/AutoIncrementVersion")
                If lNode IsNot Nothing Then
                    Return lNode.InnerText.Equals("true", StringComparison.OrdinalIgnoreCase)
                End If
                
                ' TODO: Could also check user settings for a global auto-increment preference
                
                ' For now, default to False
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsAutoIncrementEnabled error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Manifest Integration =====
        
        Private pManifestPath As String = String.Empty
        Private pIsManifestEmbeddingEnabled As Boolean = False
        
        ''' <summary>
        ''' Gets whether manifest embedding is enabled
        ''' </summary>
        Public ReadOnly Property IsManifestEmbeddingEnabled As Boolean
            Get
                Return pIsManifestEmbeddingEnabled
            End Get
        End Property
        
        ''' <summary>
        ''' Set the path to the manifest file
        ''' </summary>
        ''' <param name="vPath">Path to the manifest file (relative or absolute)</param>
        Public Sub SetManifestPath(vPath As String)
            Try
                If Not String.IsNullOrEmpty(vPath) Then
                    ' If relative path, make it relative to project directory
                    If Not System.IO.Path.IsPathRooted(vPath) Then
                        Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                        vPath = System.IO.Path.Combine(lProjectDir, vPath)
                    End If
                    
                    If File.Exists(vPath) Then
                        pManifestPath = vPath
                        pIsManifestEmbeddingEnabled = True
                        Console.WriteLine($"Manifest path set: {vPath}")
                    Else
                        Console.WriteLine($"Manifest file not found: {vPath}")
                        pManifestPath = String.Empty
                        pIsManifestEmbeddingEnabled = False
                    End If
                Else
                    ' Clear manifest path (disable embedding)
                    pManifestPath = String.Empty
                    pIsManifestEmbeddingEnabled = False
                    Console.WriteLine("Manifest embedding disabled")
                End If
            Catch ex As Exception
                Console.WriteLine($"SetManifestPath error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get the current manifest path
        ''' </summary>
        ''' <returns>Path to the manifest file or empty string if not set</returns>
        Public Function GetManifestPath() As String
            Return pManifestPath
        End Function
        
        ''' <summary>
        ''' Enable or disable manifest embedding
        ''' </summary>
        ''' <param name="vEnabled">True to enable, False to disable</param>
        Public Sub SetManifestEmbeddingEnabled(vEnabled As Boolean)
            pIsManifestEmbeddingEnabled = vEnabled
            If Not vEnabled Then
                pManifestPath = String.Empty
            End If
        End Sub
        
    End Class
    
End Namespace