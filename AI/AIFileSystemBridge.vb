' Utilities/AIFileSystemBridge.vb - Bridge for AI file system operations
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Xml
Imports System.Text 
Imports SimpleIDE.Utilities.ProjectFileParser

Namespace Utilities
    Public Class AIFileSystemBridge
        
        Private pProjectRoot As String
        Private pAllowedExtensions As New HashSet(Of String) From {
            ".vb", ".xml", ".xaml", ".json", ".config", ".resx", 
            ".txt", ".md", ".gitignore", ".vbproj", ".sln"
        }
        
        Public Property ProjectRoot As String
            Get
                Return pProjectRoot
            End Get
            Set(Value As String)
                pProjectRoot = Value
            End Set
        End Property
        
        ' Create a new VB.NET project
        Public Function CreateProject(vProjectName As String, vProjectPath As String, vProjectType As String) As String
            Try
                ' Create project directory
                Dim lProjectDir As String = Path.Combine(vProjectPath, vProjectName)
                If Directory.Exists(lProjectDir) Then
                    Return $"error: Directory already exists: {lProjectDir}"
                End If
                
                Directory.CreateDirectory(lProjectDir)
                
                ' Create project file
                Dim lProjectFile As String = Path.Combine(lProjectDir, $"{vProjectName}.vbproj")
                CreateProjectFile(lProjectFile, vProjectName, vProjectType)
                
                ' Create standard directories
                Directory.CreateDirectory(Path.Combine(lProjectDir, "My project"))
                Directory.CreateDirectory(Path.Combine(lProjectDir, "Resources"))
                
                ' Create Program.vb
                CreateProgramFile(lProjectDir, vProjectName, vProjectType)
                
                ' Create AssemblyInfo.vb
                CreateAssemblyInfo(lProjectDir, vProjectName)
                
                ' Create .gitignore
                GitIgnoreHelper.CreateGitIgnore(lProjectDir)
                
                pProjectRoot = lProjectDir
                Return lProjectFile
                
            Catch ex As Exception
                Return $"error creating project: {ex.Message}"
            End Try
        End Function
        
        ' Create a file with content
        Public Function CreateFile(vRelativePath As String, vContent As String) As Boolean
            Try
                If Not IsValidFileName(vRelativePath) Then
                    Throw New Exception($"Invalid file extension for: {vRelativePath}")
                End If
                
                Dim lFullPath As String = GetFullPath(vRelativePath)
                Dim lDirectory As String = Path.GetDirectoryName(lFullPath)
                
                If Not Directory.Exists(lDirectory) Then
                    Directory.CreateDirectory(lDirectory)
                End If
                
                File.WriteAllText(lFullPath, vContent)
                
                ' Add to project file if it's a VB file
                If Path.GetExtension(vRelativePath).ToLower() = ".vb" Then
                    AddFileToProject(vRelativePath)
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error creating file: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Read file content
        Public Function ReadFile(vRelativePath As String) As String
            Try
                Dim lFullPath As String = GetFullPath(vRelativePath)
                
                If Not File.Exists(lFullPath) Then
                    Return $"error: File not found: {vRelativePath}"
                End If
                
                Return File.ReadAllText(lFullPath)
                
            Catch ex As Exception
                Return $"error reading file: {ex.Message}"
            End Try
        End Function
        
        ' Modify existing file
        Public Function ModifyFile(vRelativePath As String, vNewContent As String) As Boolean
            Try
                If Not IsValidFileName(vRelativePath) Then
                    Throw New Exception($"Invalid file extension for: {vRelativePath}")
                End If
                
                Dim lFullPath As String = GetFullPath(vRelativePath)
                
                If Not File.Exists(lFullPath) Then
                    Throw New Exception($"File not found: {vRelativePath}")
                End If
                
                ' Create backup
                Dim lBackupPath As String = lFullPath & ".bak"
                File.Copy(lFullPath, lBackupPath, True)
                
                ' Write new content
                File.WriteAllText(lFullPath, vNewContent)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error modifying file: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Delete a file
        Public Function DeleteFile(vRelativePath As String) As Boolean
            Try
                Dim lFullPath As String = GetFullPath(vRelativePath)
                
                If File.Exists(lFullPath) Then
                    File.Delete(lFullPath)
                    
                    ' Remove from project file if it's a VB file
                    If Path.GetExtension(vRelativePath).ToLower() = ".vb" Then
                        RemoveFileFromProject(vRelativePath)
                    End If
                    
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"error deleting file: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' List files in project
        Public Function ListProjectFiles() As List(Of String)
            Dim lFiles As New List(Of String)
            
            Try
                If String.IsNullOrEmpty(pProjectRoot) OrElse Not Directory.Exists(pProjectRoot) Then
                    Return lFiles
                End If
                
                ' Find project file
                Dim lProjectFiles() As String = Directory.GetFiles(pProjectRoot, "*.vbproj")
                If lProjectFiles.Length = 0 Then
                    ' No project file, list all VB files
                    Dim lAllFiles() As String = Directory.GetFiles(pProjectRoot, "*.vb", SearchOption.AllDirectories)
                    for each lFile in lAllFiles
                        lFiles.Add(GetRelativePath(lFile))
                    Next
                Else
                    ' Parse project file
                    Dim lProjectInfo As ProjectFileParser.ProjectInfo = ProjectFileParser.ParseProjectFile(lProjectFiles(0))
                    lFiles.AddRange(lProjectInfo.CompileItems)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error listing project files: {ex.Message}")
            End Try
            
            Return lFiles
        End Function
        
        ' Get project structure as tree
        Public Function GetProjectStructure() As String
            Dim lBuilder As New StringBuilder()
            
            Try
                If String.IsNullOrEmpty(pProjectRoot) OrElse Not Directory.Exists(pProjectRoot) Then
                    Return "No project loaded"
                End If
                
                lBuilder.AppendLine(Path.GetFileName(pProjectRoot))
                BuildDirectoryTree(pProjectRoot, lBuilder, "", True)
                
            Catch ex As Exception
                lBuilder.AppendLine($"error: {ex.Message}")
            End Try
            
            Return lBuilder.ToString()
        End Function
        
        Private Sub BuildDirectoryTree(vPath As String, vBuilder As StringBuilder, vIndent As String, vIsLast As Boolean)
            ' Get directories and files
            Dim lDirs() As String = Directory.GetDirectories(vPath)
            Dim lFiles() As String = Directory.GetFiles(vPath)
            
            ' Filter out hidden and system directories
            lDirs = lDirs.Where(Function(d) Not New DirectoryInfo(d).Attributes.HasFlag(FileAttributes.Hidden)).ToArray()
            
            ' Process directories
            for i As Integer = 0 To lDirs.Length - 1
                Dim lDirName As String = Path.GetFileName(lDirs(i))
                If lDirName.StartsWith(".") Then Continue for
                
                Dim lIsLastItem As Boolean = (i = lDirs.Length - 1) AndAlso lFiles.Length = 0
                vBuilder.AppendLine($"{vIndent}{If(lIsLastItem, "└── ", "├── ")}{lDirName}/")
                
                Dim lNewIndent As String = vIndent & If(lIsLastItem, "    ", "│   ")
                BuildDirectoryTree(lDirs(i), vBuilder, lNewIndent, lIsLastItem)
            Next
            
            ' Process files
            for i As Integer = 0 To lFiles.Length - 1
                Dim lFileName As String = Path.GetFileName(lFiles(i))
                Dim lIsLastItem As Boolean = (i = lFiles.Length - 1)
                vBuilder.AppendLine($"{vIndent}{If(lIsLastItem, "└── ", "├── ")}{lFileName}")
            Next
        End Sub
        
        Private Function CreateProjectFile(vProjectFile As String, vProjectName As String, vProjectType As String) As Boolean
            Try
                Dim lContent As String = $"<project Sdk=""Microsoft.NET.Sdk"">" + Environment.NewLine
                lContent += "  <PropertyGroup>" + Environment.NewLine
                lContent += "      <OutputType>{If(vProjectType = ""Console"", ""Exe"", ""WinExe"")}</OutputType>" + Environment.NewLine
                lContent += "      <RootNamespace>{vProjectName}</RootNamespace>" + Environment.NewLine
                lContent += "      <TargetFramework>net8.0</TargetFramework>" + Environment.NewLine
                lContent += "      <OptionExplicit>On</OptionExplicit>" + Environment.NewLine
                lContent += "      <OptionCompare>Binary</OptionCompare>" + Environment.NewLine
                lContent += "      <OptionStrict>On</OptionStrict>" + Environment.NewLine
                lContent += "      <OptionInfer>On</OptionInfer>" + Environment.NewLine
                lContent += "    </PropertyGroup>" + Environment.NewLine
                lContent += "  </project>"
                
                File.WriteAllText(vProjectFile, lContent)
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error creating project file: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Sub CreateProgramFile(vProjectDir As String, vProjectName As String, vProjectType As String)
            Dim lProgramFile As String = Path.Combine(vProjectDir, "Program.vb")
            Dim lContent As String = ""
            
            Select Case vProjectType
                Case "Console"
                    lContent = $"' {vProjectName} - Console Application
Imports System

Module Program
    Sub Main(args As String())
        Console.WriteLine(""Hello, World!"")
        Console.WriteLine(""Press any key To Exit..."")
        Console.ReadKey()
    End Sub
End Module"
                
                Case "Library"
                    lContent = $"' {vProjectName} - Class Library
Imports System

Namespace {vProjectName}
    Public Class Class1
        ' TODO: Add your class implementation here
    End Class
End Namespace"
                
                Case Else ' Windows/GTK application
                    lContent = $"' {vProjectName} - Application Entry Point
Imports System

Module Program
    Sub Main(args As String())
        ' TODO: Initialize your application here
        Console.WriteLine(""Application started"")
    End Sub
End Module"
            End Select
            
            File.WriteAllText(lProgramFile, lContent)
        End Sub
        
        Private Sub CreateAssemblyInfo(vProjectDir As String, vProjectName As String)
            Dim lAssemblyInfoFile As String = Path.Combine(vProjectDir, "My project", "AssemblyInfo.vb")
            Dim lContent As String = $"Imports System.Reflection
Imports System.Runtime.InteropServices

' General Information about an assembly
<Assembly: AssemblyTitle(""{vProjectName}"")>
<Assembly: AssemblyDescription("""")>
<Assembly: AssemblyConfiguration("""")>
<Assembly: AssemblyCompany("""")>
<Assembly: AssemblyProduct(""{vProjectName}"")>
<Assembly: AssemblyCopyright(""Copyright © {DateTime.Now.Year}"")>
<Assembly: AssemblyTrademark("""")>

' Version information
<Assembly: AssemblyVersion(""1.0.0.0"")>
<Assembly: AssemblyFileVersion(""1.0.0.0"")>

' COM visibility
<Assembly: ComVisible(False)>"
            
            File.WriteAllText(lAssemblyInfoFile, lContent)
        End Sub
        
        Private Function AddFileToProject(vRelativePath As String) As Boolean
            Try
                ' Find project file
                Dim lProjectFiles() As String = Directory.GetFiles(pProjectRoot, "*.vbproj")
                If lProjectFiles.Length = 0 Then Return False
                
                Dim lProjectFile As String = lProjectFiles(0)
                Dim lDoc As New XmlDocument()
                lDoc.Load(lProjectFile)
                
                ' Check if already included
                Dim lNodes As XmlNodeList = lDoc.SelectNodes($"//Compile[@Include='{vRelativePath}']")
                If lNodes.Count > 0 Then Return True
                
                ' Add compile item
                ' For SDK-style projects, files are automatically included
                ' For older projects, we'd need to add the Compile element
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error adding file To project: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function RemoveFileFromProject(vRelativePath As String) As Boolean
            ' Implementation for removing file from project
            Return True
        End Function
        
        Private Function GetFullPath(vRelativePath As String) As String
            If String.IsNullOrEmpty(pProjectRoot) Then
                Throw New Exception("No project root Set")
            End If
            
            Return Path.Combine(pProjectRoot, vRelativePath)
        End Function
        
        Private Function GetRelativePath(vFullPath As String) As String
            If String.IsNullOrEmpty(pProjectRoot) Then
                Return vFullPath
            End If
            
            Dim lUri1 As New Uri(pProjectRoot & Path.DirectorySeparatorChar)
            Dim lUri2 As New Uri(vFullPath)
            
            Return Uri.UnescapeDataString(lUri1.MakeRelativeUri(lUri2).ToString()).Replace("/"c, Path.DirectorySeparatorChar)
        End Function
        
        Private Function IsValidFileName(vFileName As String) As Boolean
            Dim lExtension As String = Path.GetExtension(vFileName).ToLower()
            Return pAllowedExtensions.Contains(lExtension)
        End Function


        ''' <summary>
        ''' Get list of all project files
        ''' </summary>
        Public Function GetProjectFiles() As List(Of String)
            Dim lFiles As New List(Of String)
            
            Try
                If String.IsNullOrEmpty(pProjectRoot) OrElse Not Directory.Exists(pProjectRoot) Then
                    Return lFiles
                End If
                
                ' Find all VB files in the project
                Dim lVbFiles() As String = Directory.GetFiles(pProjectRoot, "*.vb", SearchOption.AllDirectories)
                
                ' Convert to relative paths
                For Each lFile In lVbFiles
                    lFiles.Add(GetRelativePath(lFile))
                Next
                
                ' Also include project file
                Dim lProjectFiles() As String = Directory.GetFiles(pProjectRoot, "*.vbproj", SearchOption.TopDirectoryOnly)
                For Each lProjectFile In lProjectFiles
                    lFiles.Add(Path.GetFileName(lProjectFile))
                Next
                
            Catch ex As Exception
                Console.WriteLine($"GetProjectFiles error: {ex.Message}")
            End Try
            
            Return lFiles
        End Function
        
    End Class
    
    ' ===== Extensions for ProjectFileParser =====
    Partial Public Class ProjectFileParser
        
        ''' <summary>
        ''' Parse a project file and return project information
        ''' </summary>
        Public Shared Function ParseProject(vProjectFilePath As String) As ProjectInfo
            ' This is an alias for the existing ParseProjectFile method
            Return ParseProjectFile(vProjectFilePath)
        End Function
        
    End Class
    
    ' ===== Extensions for ProjectInfo =====
    Partial Public Class ProjectFileParser
        
        Partial Public Class ProjectInfo
            
            Private pTargetFramework As String = "net8.0"
            Private pOutputType As String = "Exe"
            
            ''' <summary>
            ''' The target framework for the project (e.g., net8.0, net6.0)
            ''' </summary>
            Public Property TargetFramework As String
                Get
                    Return pTargetFramework
                End Get
                Set(value As String)
                    pTargetFramework = value
                End Set
            End Property
            
            ''' <summary>
            ''' The output type of the project (Exe, Library, WinExe)
            ''' </summary>
            Public Property OutputType As String
                Get
                    Return pOutputType
                End Get
                Set(value As String)
                    pOutputType = value
                End Set
            End Property
            
        End Class
        
        ' ===== Enhanced ParseProjectFile to populate new properties =====
        ' This extends the existing ParseProjectFile method
        Public Shared Function ParseProjectFileEnhanced(vProjectFilePath As String) As ProjectInfo
            Dim lInfo As ProjectInfo = ParseProjectFile(vProjectFilePath)
            
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(vProjectFilePath)
                
                ' Parse TargetFramework
                Dim lTargetFrameworkNode As XmlNode = lDoc.SelectSingleNode("//TargetFramework")
                If lTargetFrameworkNode IsNot Nothing Then
                    lInfo.TargetFramework = lTargetFrameworkNode.InnerText.Trim()
                End If
                
                ' Parse OutputType
                Dim lOutputTypeNode As XmlNode = lDoc.SelectSingleNode("//OutputType")
                If lOutputTypeNode IsNot Nothing Then
                    lInfo.OutputType = lOutputTypeNode.InnerText.Trim()
                End If
                
                ' Also check PropertyGroup with namespace
                If String.IsNullOrEmpty(lInfo.TargetFramework) OrElse String.IsNullOrEmpty(lInfo.OutputType) Then
                    Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                    lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                    
                    If String.IsNullOrEmpty(lInfo.TargetFramework) Then
                        Dim lNode As XmlNode = lDoc.SelectSingleNode("//ms:TargetFramework", lNamespaceManager)
                        If lNode IsNot Nothing Then
                            lInfo.TargetFramework = lNode.InnerText.Trim()
                        End If
                    End If
                    
                    If String.IsNullOrEmpty(lInfo.OutputType) Then
                        Dim lNode As XmlNode = lDoc.SelectSingleNode("//ms:OutputType", lNamespaceManager)
                        If lNode IsNot Nothing Then
                            lInfo.OutputType = lNode.InnerText.Trim()
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseProjectFileEnhanced error: {ex.Message}")
                ' Keep default values if parsing fails
            End Try
            
            Return lInfo
        End Function

    End Class
End Namespace
 
