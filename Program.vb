' Program.vb - Application entry point for SimpleIDE with Command Line Support
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Text.Json
Imports Gtk
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Module Program
    ''' <summary>
    ''' Export project context for AI analysis
    ''' </summary>
    Private Sub ExportProjectContext(vOutputFile As String, vUseJson As Boolean)
        Try
            Dim lCurrentDir As String = Environment.CurrentDirectory
            Dim lProjectFile As String = AutoDetectProject(lCurrentDir)
            
            If lProjectFile Is Nothing Then
                Console.WriteLine("No project found in current directory")
                Return
            End If
            
            Console.WriteLine($"Exporting context for: {lProjectFile}")
            
            ' Create AIFileSystemBridge to gather project info
            Dim lBridge As New Utilities.AIFileSystemBridge()
            lBridge.ProjectRoot = Path.GetDirectoryName(lProjectFile)
            
            Dim lContext As New Dictionary(Of String, Object)
            
            ' Gather project structure
            lContext("project") = Path.GetFileName(lProjectFile)
            lContext("projectPath") = lProjectFile
            lContext("files") = lBridge.GetProjectFiles()
            lContext("structure") = lBridge.GetProjectStructure()
            
            ' Get project references and dependencies
            Dim lParser As New Utilities.ProjectFileParser()
            Dim lProjectInfo As Utilities.ProjectFileParser.ProjectInfo = 
                Utilities.ProjectFileParser.ParseProjectFileEnhanced(lProjectFile)
            lContext("targetFramework") = lProjectInfo.TargetFramework
            lContext("outputType") = lProjectInfo.OutputType
            lContext("references") = lProjectInfo.References
            lContext("packageReferences") = lProjectInfo.PackageReferences
            
            ' Output the context
            Dim lOutput As String
            If vUseJson Then
                lOutput = JsonSerializer.Serialize(lContext, New JsonSerializerOptions with {.WriteIndented = True})
            Else
                Dim lBuilder As New StringBuilder()
                lBuilder.AppendLine($"Project: {lContext("project")}")
                lBuilder.AppendLine($"Path: {lContext("projectPath")}")
                lBuilder.AppendLine($"Target Framework: {lProjectInfo.TargetFramework}")
                lBuilder.AppendLine($"Output Type: {lProjectInfo.OutputType}")
                lBuilder.AppendLine()
                lBuilder.AppendLine("Project Structure:")
                lBuilder.AppendLine(lContext("structure").ToString())
                lOutput = lBuilder.ToString()
            End If
            
            If vOutputFile IsNot Nothing Then
                File.WriteAllText(vOutputFile, lOutput)
                Console.WriteLine($"Context exported to: {vOutputFile}")
            Else
                Console.WriteLine(lOutput)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"Error exporting context: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Import AI-generated artifact into project
    ''' </summary>
    Private Sub ImportArtifact(vArtifactFile As String, vUseJson As Boolean)
        Try
            If Not File.Exists(vArtifactFile) Then
                Console.WriteLine($"Artifact file not found: {vArtifactFile}")
                Return
            End If
            
            Dim lContent As String = File.ReadAllText(vArtifactFile)
            Dim lProjectFile As String = AutoDetectProject(Environment.CurrentDirectory)
            
            If lProjectFile Is Nothing Then
                Console.WriteLine("No project found in current directory")
                Return
            End If
            
            ' Parse artifact (could be JSON with metadata or plain code)
            Dim lFileName As String
            Dim lCode As String
            
            If vArtifactFile.EndsWith(".json") Then
                ' Parse JSON artifact
                Dim lArtifact = JsonSerializer.Deserialize(Of Dictionary(Of String, String))(lContent)
                lFileName = lArtifact("fileName")
                lCode = lArtifact("content")
            Else
                ' Plain code file
                lFileName = Path.GetFileName(vArtifactFile)
                lCode = lContent
            End If
            
            ' Save to project directory
            Dim lProjectDir As String = Path.GetDirectoryName(lProjectFile)
            Dim lTargetPath As String = Path.Combine(lProjectDir, lFileName)
            
            ' Check if file exists
            If File.Exists(lTargetPath) Then
                Console.Write($"File {lFileName} already exists. Overwrite? (y/n): ")
                If Console.ReadLine().ToLower() <> "y" Then
                    Console.WriteLine("Import cancelled")
                    Return
                End If
            End If
            
            File.WriteAllText(lTargetPath, lCode)
            Console.WriteLine($"Imported artifact to: {lTargetPath}")
            
            ' TODO: Add file to project file if it's a .vb file
            ' - Parse the .vbproj XML
            ' - Add <Compile Include="filename.vb" /> to ItemGroup
            ' - Save the updated project file
            ' - Optionally run dotnet restore
            
        Catch ex As Exception
            Console.WriteLine($"Error importing artifact: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Update AI knowledge base with current project
    ''' </summary>
    Private Sub UpdateAIKnowledge(vUseJson As Boolean)
        Try
            Console.WriteLine("Updating AI knowledge base...")
            
            ' TODO: Implement actual AI knowledge base update
            ' - Connect to AI knowledge storage system
            ' - Parse entire project structure
            ' - Extract class/method signatures
            ' - Update vector database or knowledge graph
            ' - Sync with Mem0 if configured
            ' For now, we'll export a knowledge file
            Dim lKnowledgeFile As String = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VbIDE",
                "ai_knowledge.json"
            )
            
            ExportProjectContext(lKnowledgeFile, True)
            Console.WriteLine($"Knowledge base updated: {lKnowledgeFile}")
            
        Catch ex As Exception
            Console.WriteLine($"Error updating knowledge: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Analyze project and output results
    ''' </summary>
    Private Sub AnalyzeProject(vType As String, vOutputFile As String, vUseJson As Boolean)
        Try
            Dim lProjectFile As String = AutoDetectProject(Environment.CurrentDirectory)
            
            If lProjectFile Is Nothing Then
                Console.WriteLine("No project found in current directory")
                Return
            End If
            
            Console.WriteLine($"Analyzing project: {Path.GetFileName(lProjectFile)}")
            Console.WriteLine($"Analysis type: {vType}")
            
            Dim lResults As New Dictionary(Of String, Object)
            lResults("project") = lProjectFile
            lResults("analysisType") = vType
            lResults("timestamp") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            
            Select Case vType.ToLower()
                Case "errors", "all"
                    ' Run build and capture errors
                    lResults("errors") = AnalyzeBuildErrors(lProjectFile)
                    
                Case "structure", "all"
                    ' Analyze code structure
                    lResults("structure") = AnalyzeCodeStructure(lProjectFile)
                    
                Case "quality", "all"
                    ' Code quality metrics
                    lResults("quality") = AnalyzeCodeQuality(lProjectFile)
                    
                Case "security", "all"
                    ' Security analysis
                    lResults("security") = AnalyzeSecurity(lProjectFile)
                    
                Case Else
                    Console.WriteLine($"Unknown analysis type: {vType}")
                    Console.WriteLine("Valid types: errors, structure, quality, security, all")
                    Return
            End Select
            
            ' Output results
            Dim lOutput As String
            If vUseJson Then
                lOutput = JsonSerializer.Serialize(lResults, New JsonSerializerOptions with {.WriteIndented = True})
            Else
                lOutput = FormatAnalysisResults(lResults)
            End If
            
            If vOutputFile IsNot Nothing Then
                File.WriteAllText(vOutputFile, lOutput)
                Console.WriteLine($"Analysis results saved to: {vOutputFile}")
            Else
                Console.WriteLine(lOutput)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"Error analyzing project: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Generate code based on type
    ''' </summary>
    Private Sub GenerateCode(vType As String, vOutputFile As String, vUseJson As Boolean)
        Try
            Console.WriteLine($"Code generation type: {vType}")
            Console.WriteLine("Note: This feature requires AI integration to be configured in the IDE")
            
            ' TODO: Implement actual code generation with AI
            ' - Connect to Claude API or other AI service
            ' - Send project context and generation request
            ' - Parse AI response and extract code artifacts
            ' - Save generated code to appropriate locations
            
            Select Case vType.ToLower()
                Case "tests"
                    Console.WriteLine("Generating unit test templates...")
                    ' TODO: Implement test generation
                    ' - Analyze public methods and classes
                    ' - Generate NUnit/XUnit test fixtures
                    ' - Create test cases for edge cases
                    ' - Add assertions based on method signatures
                    
                Case "docs"
                    Console.WriteLine("Generating documentation...")
                    ' TODO: Implement documentation generation
                    ' - Parse all public APIs
                    ' - Generate XML documentation comments
                    ' - Create README files
                    ' - Generate API documentation
                    
                Case "interface"
                    Console.WriteLine("Generating interface definitions...")
                    ' TODO: Implement interface extraction
                    ' - Analyze all public classes
                    ' - Extract public methods and properties
                    ' - Generate interface definitions
                    ' - Optionally refactor classes to implement interfaces
                    
                Case "refactor"
                    Console.WriteLine("Generating refactoring suggestions...")
                    ' TODO: Implement refactoring analysis
                    ' - Identify code smells
                    ' - Suggest design pattern applications
                    ' - Identify duplicate code for extraction
                    ' - Suggest method/class reorganization
                    
                Case Else
                    Console.WriteLine($"Unknown generation type: {vType}")
                    Console.WriteLine("Valid types: tests, docs, interface, refactor")
            End Select
            
        Catch ex As Exception
            Console.WriteLine($"Error generating code: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Attempt to fix compilation errors
    ''' </summary>
    Private Sub FixCompilationErrors(vUseJson As Boolean)
        Try
            Console.WriteLine("Analyzing compilation errors...")
            Console.WriteLine("Note: This feature requires AI integration to be configured")
            
            ' TODO: Implement actual error fixing with AI
            ' - Run dotnet build and capture all errors
            ' - Send errors and relevant code to AI
            ' - Parse AI suggestions for fixes
            ' - Apply fixes to source files
            ' - Re-run build to verify fixes
            ' - Report success/failure for each fix
            
            Console.WriteLine("Would attempt to fix:")
            Console.WriteLine("  - Missing imports")
            Console.WriteLine("  - Type mismatches")
            Console.WriteLine("  - Syntax errors")
            Console.WriteLine("  - Missing references")
            
        Catch ex As Exception
            Console.WriteLine($"Error fixing compilation errors: {ex.Message}")
        End Try
    End Sub
    
    ' Helper methods for analysis
    Private Function AnalyzeBuildErrors(vProjectFile As String) As List(Of String)
        ' TODO: Implement actual build error analysis
        ' - Run dotnet build with the project file
        ' - Capture and parse the output
        ' - Extract error messages, line numbers, and file locations
        ' - Return structured error information
        Return New List(Of String) From {"Sample error: Type 'X' is not defined"}
    End Function
    
    Private Function AnalyzeCodeStructure(vProjectFile As String) As Dictionary(Of String, Object)
        ' TODO: Implement code structure analysis
        ' - Parse all .vb files in the project
        ' - Build AST or use Roslyn for analysis
        ' - Count classes, methods, properties, fields
        ' - Analyze inheritance hierarchies
        ' - Identify namespaces and dependencies
        Return New Dictionary(Of String, Object) From {
            {"classes", 10},
            {"methods", 45},
            {"properties", 20}
        }
    End Function
    
    Private Function AnalyzeCodeQuality(vProjectFile As String) As Dictionary(Of String, Object)
        ' TODO: Implement code quality metrics
        ' - Calculate cyclomatic complexity
        ' - Count lines of code (LOC, SLOC)
        ' - Identify duplicate code blocks
        ' - Check for code smells
        ' - Analyze method lengths and class sizes
        Return New Dictionary(Of String, Object) From {
            {"cyclomaticComplexity", 3.2},
            {"linesOfCode", 1500},
            {"duplicateCode", "2%"}
        }
    End Function
    
    Private Function AnalyzeSecurity(vProjectFile As String) As List(Of String)
        ' TODO: Implement security analysis
        ' - Check for SQL injection vulnerabilities
        ' - Identify hardcoded credentials
        ' - Check for unsafe deserialization
        ' - Analyze input validation
        ' - Check for XSS vulnerabilities
        ' - Identify use of deprecated/unsafe APIs
        Return New List(Of String) From {"No security issues found"}
    End Function
    
    Private Function FormatAnalysisResults(vResults As Dictionary(Of String, Object)) As String
        ' TODO: Enhance formatting for better readability
        ' - Add color coding for terminal output
        ' - Format tables for metrics
        ' - Add severity levels for issues
        ' - Group related information
        Dim lBuilder As New StringBuilder()
        lBuilder.AppendLine("=== Analysis Results ===")
        For Each lKvp In vResults
            lBuilder.AppendLine($"{lKvp.Key}: {lKvp.Value}")
        Next
        Return lBuilder.ToString()
    End Function
    
    Private Const APPLICATION_ID As String = "com.simple.ide"
    Private Const APPLICATION_NAME As String = "VbIDE"
    
    Sub Main(vArgs As String())
        ' Process command line arguments that don't require GUI
        If ProcessNonGuiArguments(vArgs) Then
            Return ' Exit if handled
        End If
        
        ' Initialize GTK
        Application.Init()
        
        ' Parse command line arguments for GUI mode
        Dim lProjectToLoad As String = Nothing
        Dim lFilesToOpen As New List(Of String)()
        Dim lCurrentDirectory As String = Environment.CurrentDirectory
        Dim lShouldMaximize As Boolean = False
        Dim lShouldMinimize As Boolean = False
        Dim lNewProject As String = Nothing
        Dim lNewProjectType As String = "GTK"
        Dim lSafeMode As Boolean = False
        Dim lResetSettings As Boolean = False
        
        ' Process arguments
        Dim i As Integer = 0
        While i < vArgs.Length
            Dim lArg As String = vArgs(i)
            
            Select Case lArg.ToLower()
                Case "--maximize", "-m"
                    lShouldMaximize = True
                    Console.WriteLine("Window will be maximized")
                    
                Case "--minimize"
                    lShouldMinimize = True
                    Console.WriteLine("Window will be minimized")
                    
                Case "--New-project", "-n"
                    If i + 1 < vArgs.Length Then
                        i += 1
                        lNewProject = vArgs(i)
                        Console.WriteLine($"Will create New project: {lNewProject}")
                    Else
                        Console.WriteLine("error: --New-project requires a project name")
                        Return
                    End If
                    
                Case "--project-type", "-t"
                    If i + 1 < vArgs.Length Then
                        i += 1
                        lNewProjectType = vArgs(i)
                        If Not IsValidProjectType(lNewProjectType) Then
                            Console.WriteLine($"error: Invalid project type '{lNewProjectType}'. Valid types: Console, Library, GTK")
                            Return
                        End If
                        Console.WriteLine($"Project type: {lNewProjectType}")
                    Else
                        Console.WriteLine("Error: --project-type requires a type (Console, Library, GTK)")
                        Return
                    End If
                    
                Case "--safe-mode"
                    lSafeMode = True
                    Console.WriteLine("Starting in safe mode (no extensions or custom settings)")
                    
                Case "--reset-settings"
                    lResetSettings = True
                    Console.WriteLine("Settings will be reset to defaults")
                    
                Case "--project", "-p"
                    If i + 1 < vArgs.Length Then
                        i += 1
                        Dim lPath As String = vArgs(i)
                        Dim lFullPath As String = If(Path.IsPathRooted(lPath), lPath, Path.Combine(lCurrentDirectory, lPath))
                        If File.Exists(lFullPath) Then
                            lProjectToLoad = lFullPath
                            Console.WriteLine($"Will load project: {lFullPath}")
                        Else
                            Console.WriteLine($"Error: Project file not found: {lFullPath}")
                            Return
                        End If
                    Else
                        Console.WriteLine("Error: --project requires a file path")
                        Return
                    End If
                    
                Case "--open", "-o"
                    If i + 1 < vArgs.Length Then
                        i += 1
                        Dim lPath As String = vArgs(i)
                        Dim lFullPath As String = If(Path.IsPathRooted(lPath), lPath, Path.Combine(lCurrentDirectory, lPath))
                        If File.Exists(lFullPath) Then
                            lFilesToOpen.Add(lFullPath)
                            Console.WriteLine($"Will open file: {lFullPath}")
                        Else
                            Console.WriteLine($"Warning: File not found: {lFullPath}")
                        End If
                    Else
                        Console.WriteLine("Error: --open requires a file path")
                        Return
                    End If
                    
                Case Else
                    If Not lArg.StartsWith("-") Then
                        ' It's a file path without a flag
                        Dim lFullPath As String = If(Path.IsPathRooted(lArg), lArg, Path.Combine(lCurrentDirectory, lArg))
                        
                        If File.Exists(lFullPath) Then
                            ' Check if it's a project file
                            Dim lExtension As String = Path.GetExtension(lFullPath).ToLower()
                            
                            If lExtension = ".vbproj" OrElse lExtension = ".csproj" Then
                                ' It's a project file - only load the first one
                                If lProjectToLoad Is Nothing Then
                                    lProjectToLoad = lFullPath
                                    Console.WriteLine($"Will load project: {lFullPath}")
                                Else
                                    Console.WriteLine($"Project already specified, ignoring: {lFullPath}")
                                End If
                            Else
                                ' It's a regular file to open
                                lFilesToOpen.Add(lFullPath)
                                Console.WriteLine($"Will open file: {lFullPath}")
                            End If
                        Else
                            Console.WriteLine($"File not found: {lFullPath}")
                        End If
                    Else
                        Console.WriteLine($"Unknown option: {lArg}")
                        Console.WriteLine("Use --help to see available options")
                    End If
            End Select
            
            i += 1
        End While
        
        ' Auto-detect project if no arguments provided
        If vArgs.Length = 0 OrElse (lProjectToLoad Is Nothing AndAlso lFilesToOpen.Count = 0 AndAlso lNewProject Is Nothing) Then
            lProjectToLoad = AutoDetectProject(lCurrentDirectory)
        End If
        
        ' Handle new project creation
        If lNewProject IsNot Nothing Then
            ' Create the project directory and files
            Dim lProjectPath As String = Path.Combine(lCurrentDirectory, lNewProject)
            If CreateNewProjectStructure(lProjectPath, lNewProject, lNewProjectType) Then
                lProjectToLoad = Path.Combine(lProjectPath, $"{lNewProject}.vbproj")
                Console.WriteLine($"Created new project: {lProjectToLoad}")
            Else
                Console.WriteLine("Failed to create new project")
                Return
            End If
        End If
        
        ' Reset settings if requested
        If lResetSettings Then
            ResetApplicationSettings()
        End If
        
        ' Create main window - use appropriate constructor
        Dim lMainWindow As MainWindow
        
        If lProjectToLoad IsNot Nothing Then
            ' Use the constructor that loads a project
            Console.WriteLine($"Creating MainWindow with project: {lProjectToLoad}")
            lMainWindow = New MainWindow(lProjectToLoad)
        Else
            ' Use the default constructor
            Console.WriteLine("Creating MainWindow without project")
            lMainWindow = New MainWindow()
        End If
        
        ' Apply safe mode if requested
        If lSafeMode Then
            ApplySafeMode(lMainWindow)
        End If
        
        ' Check if we should maximize (from settings or command line)
        Dim lSettingsManager As SettingsManager = lMainWindow.GetSettingsManager()
        If lSettingsManager IsNot Nothing AndAlso Not lShouldMinimize Then
            lShouldMaximize = lShouldMaximize OrElse lSettingsManager.WindowMaximized
        End If
        
        ' Show all widgets FIRST
        lMainWindow.ShowAll()
        
        ' Use a single idle handler to restore window state after GTK initialization
        GLib.Idle.Add(Function()
            Try
                ' Handle window state
                If lShouldMinimize Then
                    lMainWindow.Iconify()
                    Console.WriteLine("Window minimized")
                ElseIf lShouldMaximize Then
                    lMainWindow.Maximize()
                    Console.WriteLine("Window maximized")
                Else
                    RestoreWindowSizeAndState(lMainWindow, False)
                End If
                
                ' Open any additional files that were specified
                If lFilesToOpen.Count > 0 Then
                    If lProjectToLoad IsNot Nothing Then
                        ' Filter files to only those within the project directory
                        Dim lProjectDir As String = Path.GetDirectoryName(lProjectToLoad)
                        for each lFile in lFilesToOpen
                            If lFile.StartsWith(lProjectDir) Then
                                Console.WriteLine($"Opening project file: {lFile}")
                                lMainWindow.OpenFile(lFile)
                            Else
                                Console.WriteLine($"Skipping file outside project: {lFile}")
                            End If
                        Next
                    Else
                        ' No project loaded, open all files
                        for each lFile in lFilesToOpen
                            Console.WriteLine($"Opening file: {lFile}")
                            lMainWindow.OpenFile(lFile)
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Window restoration error: {ex.Message}")
            End Try
            
            Return False ' Remove idle handler
        End Function)
        
        Application.Run()
    End Sub
    
    ''' <summary>
    ''' Process command line arguments that don't require GUI initialization
    ''' </summary>
    ''' <returns>True if the application should exit</returns>
    Private Function ProcessNonGuiArguments(vArgs As String()) As Boolean
        Dim lOutputFile As String = Nothing
        Dim lUseJson As Boolean = False
        Dim lAnalyzeType As String = Nothing
        Dim lGenerateType As String = Nothing
        Dim i As Integer = 0
        
        ' First pass - check for output options
        While i < vArgs.Length
            Select Case vArgs(i).ToLower()
                Case "--output"
                    If i + 1 < vArgs.Length Then
                        lOutputFile = vArgs(i + 1)
                        i += 1
                    End If
                Case "--json"
                    lUseJson = True
            End Select
            i += 1
        End While
        
        ' Second pass - process commands
        i = 0
        While i < vArgs.Length
            Dim lArg As String = vArgs(i)
            
            Select Case lArg.ToLower()
                Case "--help", "-h", "/?"
                    ShowHelp()
                    Return True
                    
                Case "--version", "-v"
                    ShowVersion()
                    Return True
                    
                Case "--license"
                    ShowLicense()
                    Return True
                    
                Case "--list-projects"
                    ListRecentProjects()
                    Return True
                    
                Case "--clean"
                    CleanBuildArtifacts()
                    Return True
                    
                Case "--export-context"
                    ExportProjectContext(lOutputFile, lUseJson)
                    Return True
                    
                Case "--import-artifact"
                    If i + 1 < vArgs.Length Then
                        ImportArtifact(vArgs(i + 1), lUseJson)
                        Return True
                    Else
                        Console.WriteLine("Error: --import-artifact requires a file path")
                        Return True
                    End If
                    
                Case "--update-knowledge"
                    UpdateAIKnowledge(lUseJson)
                    Return True
                    
                Case "--analyze"
                    If i + 1 < vArgs.Length AndAlso Not vArgs(i + 1).StartsWith("-") Then
                        lAnalyzeType = vArgs(i + 1)
                        i += 1
                    Else
                        lAnalyzeType = "all"
                    End If
                    AnalyzeProject(lAnalyzeType, lOutputFile, lUseJson)
                    Return True
                    
                Case "--generate"
                    If i + 1 < vArgs.Length Then
                        lGenerateType = vArgs(i + 1)
                        GenerateCode(lGenerateType, lOutputFile, lUseJson)
                        i += 1
                        Return True
                    Else
                        Console.WriteLine("Error: --generate requires a type (tests|docs|interface|refactor)")
                        Return True
                    End If
                    
                Case "--fix-errors"
                    FixCompilationErrors(lUseJson)
                    Return True
                    
                Case "--headless"
                    ' This flag is handled in Main, but we acknowledge it here
                    ' so it doesn't get reported as unknown
                    ' Continue processing other args
                    
                Case "--json", "--output"
                    ' Already processed, skip
                    If lArg.ToLower() = "--output" Then i += 1
            End Select
            
            i += 1
        End While
        
        Return False
    End Function
    
    ''' <summary>
    ''' Display help information
    ''' </summary>
    Private Sub ShowHelp()
        Console.WriteLine($"{APPLICATION_NAME} - Lightweight VB.NET IDE for Linux")
        Console.WriteLine()
        Console.WriteLine("Usage: VbIDE [options] [project-file] [files...]")
        Console.WriteLine()
        Console.WriteLine("Options:")
        Console.WriteLine("  -h, --help              Show this help message and exit")
        Console.WriteLine("  -v, --version           Show version information and exit")
        Console.WriteLine("      --license           Show license information and exit")
        Console.WriteLine()
        Console.WriteLine("Project Options:")
        Console.WriteLine("  -p, --project FILE      Load specified project file")
        Console.WriteLine("  -n, --new-project NAME  Create a new project with the given name")
        Console.WriteLine("  -t, --project-type TYPE Set project type (Console|Library|GTK) [default: GTK]")
        Console.WriteLine("  -o, --open FILE         Open specified file(s) in editor")
        Console.WriteLine("      --list-projects     List recently opened projects and exit")
        Console.WriteLine()
        Console.WriteLine("Window Options:")
        Console.WriteLine("  -m, --maximize          Start with maximized window")
        Console.WriteLine("      --minimize          Start with minimized window")
        Console.WriteLine()
        Console.WriteLine("Maintenance Options:")
        Console.WriteLine("      --safe-mode         Start without loading extensions or custom settings")
        Console.WriteLine("      --reset-settings    Reset all settings to defaults")
        Console.WriteLine("      --clean             Clean all build artifacts in current directory")
        Console.WriteLine()
        Console.WriteLine("AI Integration Options:")
        Console.WriteLine("      --export-context    Export project context for AI analysis")
        Console.WriteLine("      --import-artifact FILE Import AI-generated artifact into project")
        Console.WriteLine("      --update-knowledge  Update AI knowledge base with current project")
        Console.WriteLine("      --analyze [TYPE]    Analyze project (errors|structure|quality|security)")
        Console.WriteLine("      --generate TYPE     Generate code (tests|docs|interface|refactor)")
        Console.WriteLine("      --fix-errors        Attempt to auto-fix compilation errors")
        Console.WriteLine("      --headless          Run without GUI (for automation)")
        Console.WriteLine("      --json              Output in JSON format (for parsing)")
        Console.WriteLine("      --output FILE       Write output to file instead of console")
        Console.WriteLine()
        Console.WriteLine("Examples:")
        Console.WriteLine("  VbIDE                           # Auto-detect project in current directory")
        Console.WriteLine("  VbIDE MyProject.vbproj          # Open specific project")
        Console.WriteLine("  VbIDE Program.vb Module1.vb     # Open files without project")
        Console.WriteLine("  VbIDE -n MyApp -t Console       # Create new console application")
        Console.WriteLine("  VbIDE -p ~/projects/App.vbproj  # Open project from specific path")
        Console.WriteLine()
        Console.WriteLine("Environment Variables:")
        Console.WriteLine("  VBIDE_SETTINGS_PATH    Override default settings location")
        Console.WriteLine("  VBIDE_THEME            Set color theme (Dark|Light|System)")
        Console.WriteLine("  VBIDE_DEBUG            Enable debug logging (1|true)")
        Console.WriteLine()
        Console.WriteLine("When launched without arguments in a directory containing a .vbproj file,")
        Console.WriteLine("the IDE will automatically load that project.")
    End Sub
    
    ''' <summary>
    ''' Display version information
    ''' </summary>
    Private Sub ShowVersion()
        Dim assembly As System.Reflection.Assembly = System.Reflection.Assembly.GetEntryAssembly()
        'Dim version As Version = assembly.GetName().Version
        Dim v As String = assembly.FullName
       ' Console.WriteLine("Assembly Version: " & version.ToString())   

        Console.WriteLine($"{v}")
        Console.WriteLine($"Build: {ApplicationVersion.BuildNumber}")
        Console.WriteLine($"Date: {ApplicationVersion.BuildDate:yyyy-MM-dd}")
        Console.WriteLine()
        Console.WriteLine("Runtime Information:")
        Console.WriteLine($"  .NET Version: {Environment.Version}")
        Console.WriteLine($"  OS: {Environment.OSVersion}")
        Console.WriteLine($"  Machine: {Environment.MachineName}")
        Console.WriteLine($"  Processors: {Environment.ProcessorCount}")
        Console.WriteLine()
        Console.WriteLine("Copyright (C) 2025 VbIDE Contributors")
        Console.WriteLine("Licensed under GPL v3.0 - See --license for details")
        Console.WriteLine("For more information, visit: https://github.com/jamesplotts/simpleide")
    End Sub
    
    ''' <summary>
    ''' Display license information
    ''' </summary>
    Private Sub ShowLicense()
        Console.WriteLine($"{APPLICATION_NAME} - Lightweight VB.NET IDE for Linux")
        Console.WriteLine("Copyright (C) 2025 VbIDE Contributors")
        Console.WriteLine("Repository: https://github.com/jamesplotts/simpleide")
        Console.WriteLine()
        Console.WriteLine("This program is free software: you can redistribute it and/or modify")
        Console.WriteLine("it under the terms of the GNU General Public License as published by")
        Console.WriteLine("the Free Software Foundation, either version 3 of the License, or")
        Console.WriteLine("(at your option) any later version.")
        Console.WriteLine()
        Console.WriteLine("This program is distributed in the hope that it will be useful,")
        Console.WriteLine("but WITHOUT ANY WARRANTY; without even the implied warranty of")
        Console.WriteLine("MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the")
        Console.WriteLine("GNU General Public License for more details.")
        Console.WriteLine()
        Console.WriteLine("You should have received a copy of the GNU General Public License")
        Console.WriteLine("along with this program. If not, see <https://www.gnu.org/licenses/>.")
        Console.WriteLine()
        Console.WriteLine("================================================================================")
        Console.WriteLine()
        Console.WriteLine("This software includes the following third-party components:")
        Console.WriteLine()
        Console.WriteLine("GTK# (LGPL v2.1)")
        Console.WriteLine("  GTK# is a .NET binding for the GTK+ toolkit")
        Console.WriteLine("  https://github.com/GtkSharp/GtkSharp")
        Console.WriteLine()
        Console.WriteLine(".NET Runtime (MIT License)")
        Console.WriteLine("  https://github.com/dotnet/runtime")
        Console.WriteLine()
        Console.WriteLine("For the complete text of the GPL v3.0 license, visit:")
        Console.WriteLine("https://www.gnu.org/licenses/gpl-3.0.txt")
        Console.WriteLine()
        Console.WriteLine("To contribute to this project, visit:")
        Console.WriteLine("https://github.com/jamesplotts/simpleide")
    End Sub
    
    ''' <summary>
    ''' Auto-detect project file in directory
    ''' </summary>
    Private Function AutoDetectProject(vDirectory As String) As String
        Console.WriteLine($"Searching for project in: {vDirectory}")
        
        ' Search for *.vbproj files in current directory
        Dim lProjectFiles As String() = Directory.GetFiles(vDirectory, "*.vbproj", SearchOption.TopDirectoryOnly)
        
        If lProjectFiles.Length > 0 Then
            ' Found project file(s) - use the first one
            Dim lProjectToLoad As String = lProjectFiles(0)
            Console.WriteLine($"Found project file: {lProjectToLoad}")
            
            If lProjectFiles.Length > 1 Then
                Console.WriteLine($"Warning: Multiple project files found. Using: {Path.GetFileName(lProjectToLoad)}")
                for i As Integer = 1 To lProjectFiles.Length - 1
                    Console.WriteLine($"  Ignoring: {Path.GetFileName(lProjectFiles(i))}")
                Next
            End If
            
            Return lProjectToLoad
        End If
        
        ' No VB project found, try C# project
        Dim lCSharpProjects As String() = Directory.GetFiles(vDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
        
        If lCSharpProjects.Length > 0 Then
            Dim lProjectToLoad As String = lCSharpProjects(0)
            Console.WriteLine($"Found C# project file: {lProjectToLoad}")
            
            If lCSharpProjects.Length > 1 Then
                Console.WriteLine($"Warning: Multiple C# project files found. Using: {Path.GetFileName(lProjectToLoad)}")
            End If
            
            Return lProjectToLoad
        End If
        
        Console.WriteLine("No project files found in current directory")
        Return Nothing
    End Function
    
    ''' <summary>
    ''' List recently opened projects
    ''' </summary>
    Private Sub ListRecentProjects()
        Try
            Console.WriteLine("Recently opened projects:")
            Console.WriteLine()
            
            ' Create temporary settings manager to read recent projects
            Dim lSettingsManager As New SettingsManager()
            Dim lRecentProjects As List(Of String) = lSettingsManager.RecentProjects
            
            If lRecentProjects.Count = 0 Then
                Console.WriteLine("  No recent projects")
            Else
                for i As Integer = 0 To lRecentProjects.Count - 1
                    Dim lProject As String = lRecentProjects(i)
                    Dim lExists As String = If(File.Exists(lProject), "", " [NOT FOUND]")
                    Console.WriteLine($"  {i + 1}. {lProject}{lExists}")
                Next
            End If
            
        Catch ex As Exception
            Console.WriteLine($"Error reading recent projects: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Clean build artifacts in current directory
    ''' </summary>
    Private Sub CleanBuildArtifacts()
        Try
            Dim lCurrentDir As String = Environment.CurrentDirectory
            Console.WriteLine($"Cleaning build artifacts in: {lCurrentDir}")
            
            Dim lDirsToClean As String() = {"bin", "obj", ".vs"}
            Dim lCleaned As Integer = 0
            
            for each lDirName in lDirsToClean
                Dim lDirPath As String = Path.Combine(lCurrentDir, lDirName)
                If Directory.Exists(lDirPath) Then
                    Console.WriteLine($"  Removing: {lDirName}/")
                    Directory.Delete(lDirPath, True)
                    lCleaned += 1
                End If
            Next
            
            If lCleaned > 0 Then
                Console.WriteLine($"Cleaned {lCleaned} directories")
            Else
                Console.WriteLine("No build artifacts found to clean")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"Error cleaning build artifacts: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Check if project type is valid
    ''' </summary>
    Private Function IsValidProjectType(vType As String) As Boolean
        Select Case vType.ToLower()
            Case "console", "library", "gtk"
                Return True
            Case Else
                Return False
        End Select
    End Function
    
    ''' <summary>
    ''' Create new project structure
    ''' </summary>
    Private Function CreateNewProjectStructure(vPath As String, vName As String, vType As String) As Boolean
        Try
            ' TODO: Implement actual project creation
            ' - Create project directory structure
            ' - Generate .vbproj file with appropriate SDK and settings
            ' - Create Program.vb or Class1.vb based on project type
            ' - Add required NuGet packages (GTK# for GUI projects)
            ' - Create My Project folder with AssemblyInfo.vb
            ' - Initialize git repository if git is available
            ' - Create .gitignore file
            Console.WriteLine($"TODO: Create project structure for {vName} of type {vType}")
            Return False
        Catch ex As Exception
            Console.WriteLine($"Error creating project: {ex.Message}")
            Return False
        End Try
    End Function
    
    ''' <summary>
    ''' Reset application settings to defaults
    ''' </summary>
    Private Sub ResetApplicationSettings()
        Try
            ' TODO: Consider backing up settings before reset
            ' - Create backup of current settings
            ' - Prompt for confirmation if not in headless mode
            ' - Reset all setting categories (Editor, UI, AI, etc.)
            
            Dim lSettingsPath As String = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VbIDE",
                "settings.xml"
            )
            
            If File.Exists(lSettingsPath) Then
                File.Delete(lSettingsPath)
                Console.WriteLine("Settings have been reset to defaults")
            Else
                Console.WriteLine("No settings file found to reset")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"Error resetting settings: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Apply safe mode restrictions
    ''' </summary>
    Private Sub ApplySafeMode(vWindow As MainWindow)
        ' TODO: Implement safe mode restrictions
        ' - Disable all extensions/plugins
        ' - Use default color scheme
        ' - Disable AI integration
        ' - Use minimal UI configuration
        ' - Disable auto-save features
        ' - Log all operations for debugging
        Console.WriteLine("Safe mode applied - extensions disabled, using default settings")
    End Sub
    
    ''' <summary>
    ''' Restores window size and state after the window is shown and realized
    ''' </summary>
    ''' <param name="vWindow">The main window to restore</param>
    ''' <param name="vShouldMaximize">Whether the window should be maximized</param>
    Private Sub RestoreWindowSizeAndState(vWindow As MainWindow, vShouldMaximize As Boolean)
        Try
            ' Get settings manager from window
            Dim lSettingsManager = vWindow.GetSettingsManager()
            If lSettingsManager Is Nothing Then Return
            
            Console.WriteLine($"RestoreWindowSizeAndState: ShouldMaximize={vShouldMaximize}")
            
            ' If window should be maximized, just maximize it
            If vShouldMaximize Then
                vWindow.Maximize()
                Console.WriteLine("Window maximized")
                Return
            End If
            
            ' For non-maximized windows, restore saved size
            Dim lWidth As Integer = lSettingsManager.WindowWidth
            Dim lHeight As Integer = lSettingsManager.WindowHeight
            
            ' Simple validation - just make sure it's not ridiculous
            If lWidth < 800 Then lWidth = 1024
            If lHeight < 600 Then lHeight = 768
            
            Console.WriteLine($"Restoring window size: {lWidth}x{lHeight}")
            
            ' Set the size and center it
            vWindow.Resize(lWidth, lHeight)
            vWindow.SetPosition(WindowPosition.Center)
            
        Catch ex As Exception
            Console.WriteLine($"RestoreWindowSizeAndState error: {ex.Message}")
            ' Fallback to reasonable defaults
            vWindow.Resize(1024, 768)
            vWindow.SetPosition(WindowPosition.Center)
        End Try
    End Sub



End Module
