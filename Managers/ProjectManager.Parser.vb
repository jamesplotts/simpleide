' ProjectManager.Parser.vb - Integration of ProjectParser with ProjectManager
' Created: 2025-08-25

Imports System
Imports System.IO
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Managers
    
    ''' <summary>
    ''' ProjectManager extension for project-wide parsing functionality
    ''' </summary>
    Partial Public Class ProjectManager
        
        ' ===== Private Fields =====
        Private pProjectParser As ProjectParser
        'Private pProjectSyntaxTree As SyntaxNode
        Private pLastParseTime As DateTime
        Private pParseErrors As List(Of String)
        
        ' ===== Events =====
        
        
        ''' <summary>
        ''' Raised when parse errors occur
        ''' </summary>
        Public Event ParseErrorsOccurred(vErrors As List(Of String))
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets the project's unified syntax tree
        ''' </summary>
        Public ReadOnly Property ProjectSyntaxTree As SyntaxNode
            Get
                Return pProjectSyntaxTree
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the last time the project was parsed
        ''' </summary>
        Public ReadOnly Property LastParseTime As DateTime
            Get
                Return pLastParseTime
            End Get
        End Property
        
        ''' <summary>
        ''' Gets any parse errors from the last parse operation
        ''' </summary>
        Public ReadOnly Property ParseErrors As List(Of String)
            Get
                Return If(pParseErrors, New List(Of String)())
            End Get
        End Property
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Parses all source files in the project into a unified syntax tree
        ''' </summary>
        Public Function ParseProjectStructure() As Boolean
            Try
                Console.WriteLine("ProjectManager: Starting project structure parse...")
                
                ' Ensure we have a project loaded
                If Not pIsProjectOpen OrElse pCurrentProjectInfo Is Nothing Then
                    Console.WriteLine("ProjectManager: No project is currently loaded")
                    Return False
                End If
                
                ' Ensure all source files are loaded
                If Not LoadAllSourceFiles() Then
                    Console.WriteLine("ProjectManager: Failed to load source files")
                    Return False
                End If
                
                ' Initialize parser if needed
                InitializeParser()
                
                ' Parse the project
                pProjectSyntaxTree = pProjectParser.ParseProject()
                
                If pProjectParser IsNot Nothing Then
                    pParseErrors = pProjectParser.GetParseErrors()
                End If
                
                pLastParseTime = DateTime.Now
                
                ' Check if we actually got a valid tree
                If pProjectSyntaxTree IsNot Nothing Then
                    Console.WriteLine($"ProjectManager: ParseProject returned tree with root: {pProjectSyntaxTree.Name}")
                    Console.WriteLine($"  Root type: {pProjectSyntaxTree.NodeType}")
                    Console.WriteLine($"  Children count: {pProjectSyntaxTree.Children.Count}")
                    
                    ' Log first level children
                    For Each lChild In pProjectSyntaxTree.Children
                        Console.WriteLine($"    - {lChild.Name} ({lChild.NodeType})")
                    Next
                Else
                    Console.WriteLine("ProjectManager: ParseProject returned Nothing!")
                End If
                
                ' Raise events
                If pProjectSyntaxTree IsNot Nothing Then
                    RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                    Console.WriteLine("ProjectManager: Raised ProjectStructureLoaded event")
                End If
                
                If pParseErrors IsNot Nothing AndAlso pParseErrors.Count > 0 Then
                    RaiseEvent ParseErrorsOccurred(pParseErrors)
                End If
                
                ' Log results
                Dim lNodeCount As Integer = CountNodes(pProjectSyntaxTree)
                Console.WriteLine($"ProjectManager: Parse complete. Total nodes: {lNodeCount}")
                If pParseErrors IsNot Nothing AndAlso pParseErrors.Count > 0 Then
                    Console.WriteLine($"ProjectManager: {pParseErrors.Count} parse errors encountered")
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.ParseProjectStructure error: {ex.Message}")
                Console.WriteLine($"  Stack trace: {ex.StackTrace}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Rebuilds the project syntax tree
        ''' </summary>
        Public Sub RebuildProjectTree()
            Try
                Console.WriteLine("ProjectManager: Rebuilding project tree...")
                ParseProjectStructure()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RebuildProjectTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Loads the project structure (includes parsing)
        ''' </summary>
        Public Function LoadProjectStructure() As Boolean
            Try
                Console.WriteLine("ProjectManager: Loading project structure...")
                
                ' Load all source files first
                If Not LoadAllSourceFiles() Then
                    Console.WriteLine("ProjectManager: Failed to load source files")
                    Return False
                End If
                
                ' Parse the structure
                Return ParseProjectStructure()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProjectStructure error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Gets a specific node from the project tree by its fully qualified name
        ''' </summary>
        Public Function GetNodeByFullName(vFullName As String) As SyntaxNode
            Try
                If pProjectSyntaxTree Is Nothing OrElse String.IsNullOrEmpty(vFullName) Then
                    Return Nothing
                End If
                
                Return FindNodeByFullName(pProjectSyntaxTree, vFullName)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetNodeByFullName error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all nodes of a specific type from the project tree
        ''' </summary>
        Public Function GetNodesOfType(vNodeType As CodeNodeType) As List(Of SyntaxNode)
            Try
                Dim lResults As New List(Of SyntaxNode)()
                
                If pProjectSyntaxTree IsNot Nothing Then
                    CollectNodesOfType(pProjectSyntaxTree, vNodeType, lResults)
                End If
                
                Return lResults
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetNodesOfType error: {ex.Message}")
                Return New List(Of SyntaxNode)()
            End Try
        End Function
        
        ''' <summary>
        ''' Refreshes the parse for a specific file
        ''' </summary>
        Public Function RefreshFileParse(vFilePath As String) As Boolean
            Try
                If String.IsNullOrEmpty(vFilePath) Then
                    Return False
                End If
                
                ' Get the SourceFileInfo
                Dim lSourceFileInfo As SourceFileInfo = GetSourceFileInfo(vFilePath)
                If lSourceFileInfo Is Nothing Then
                    Console.WriteLine($"ProjectManager: File not found in project: {vFilePath}")
                    Return False
                End If
                
                ' Reload content
                If Not lSourceFileInfo.LoadContent() Then
                    Console.WriteLine($"ProjectManager: Failed to reload content for: {vFilePath}")
                    Return False
                End If
                
                ' Mark for reparsing
                lSourceFileInfo.NeedsParsing = True
                
                ' Rebuild the entire tree (future optimization: incremental update)
                Return ParseProjectStructure()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RefreshFileParse error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Loads all source files in the project
        ''' </summary>
        ''' <returns>True if all files loaded successfully</returns>
        Private Function LoadAllSourceFiles() As Boolean
            Try
                Console.WriteLine("ProjectManager: Loading all source files...")
                
                If pCurrentProjectInfo Is Nothing Then
                    Console.WriteLine("ProjectManager: No project info available")
                    Return False
                End If
                
                ' Get list of source files
                Dim lSourceFilePaths As List(Of String) = pCurrentProjectInfo.SourceFiles
                Console.WriteLine($"ProjectManager: Found {lSourceFilePaths.Count} source files to load")
                
                If lSourceFilePaths.Count = 0 Then
                    Console.WriteLine("ProjectManager: No source files in project")
                    Return False
                End If
                
                ' Ensure pSourceFiles dictionary is initialized
                If pSourceFiles Is Nothing Then
                    pSourceFiles = New Dictionary(Of String, SourceFileInfo)()
                End If
                
                Dim lSuccessCount As Integer = 0
                Dim lFailedFiles As New List(Of String)()
                
                ' Load each file
                For Each lFilePath In lSourceFilePaths
                    Try
                        ' Check if already loaded
                        Dim lSourceFile As SourceFileInfo = Nothing
                        
                        If pSourceFiles.ContainsKey(lFilePath) Then
                            lSourceFile = pSourceFiles(lFilePath)
                            If lSourceFile.IsLoaded Then
                                lSuccessCount += 1
                                Continue For
                            End If
                        Else
                            ' Create new SourceFileInfo
                            lSourceFile = New SourceFileInfo(lFilePath, pCurrentProjectInfo.ProjectDirectory)
                            lSourceFile.ProjectRootNamespace = pCurrentProjectInfo.GetEffectiveRootNamespace()
                            pSourceFiles(lFilePath) = lSourceFile
                        End If
                        
                        ' Load the file
                        If lSourceFile.LoadContent() Then
                            lSuccessCount += 1
                            Console.WriteLine($"  Loaded: {Path.GetFileName(lFilePath)} ({lSourceFile.Content.Length} chars)")
                        Else
                            lFailedFiles.Add(lFilePath)
                            Console.WriteLine($"  Failed: {Path.GetFileName(lFilePath)}")
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"  Error loading {Path.GetFileName(lFilePath)}: {ex.Message}")
                        lFailedFiles.Add(lFilePath)
                    End Try
                Next
                
                Console.WriteLine($"ProjectManager: Loaded {lSuccessCount}/{lSourceFilePaths.Count} files successfully")
                
                If lFailedFiles.Count > 0 Then
                    Console.WriteLine($"ProjectManager: Failed to load {lFailedFiles.Count} files:")
                    For Each lPath In lFailedFiles.Take(5)
                        Console.WriteLine($"  - {Path.GetFileName(lPath)}")
                    Next
                End If
                
                ' Return true if at least some files loaded
                Return lSuccessCount > 0
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadAllSourceFiles error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Finds a node by its fully qualified name
        ''' </summary>
        Private Function FindNodeByFullName(vNode As SyntaxNode, vFullName As String) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing
                
                ' Check if this node matches
                If vNode.GetFullyQualifiedName().Equals(vFullName, StringComparison.OrdinalIgnoreCase) Then
                    Return vNode
                End If
                
                ' Search children
                for each lChild in vNode.Children
                    Dim lResult As SyntaxNode = FindNodeByFullName(lChild, vFullName)
                    If lResult IsNot Nothing Then
                        Return lResult
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.FindNodeByFullName error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Collects all nodes of a specific type
        ''' </summary>
        Private Sub CollectNodesOfType(vNode As SyntaxNode, vNodeType As CodeNodeType, vResults As List(Of SyntaxNode))
            Try
                If vNode Is Nothing Then Return
                
                If vNode.NodeType = vNodeType Then
                    vResults.Add(vNode)
                End If
                
                for each lChild in vNode.Children
                    CollectNodesOfType(lChild, vNodeType, vResults)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.CollectNodesOfType error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Counts total nodes in a tree
        ''' </summary>
        Private Function CountNodes(vNode As SyntaxNode) As Integer
            Try
                If vNode Is Nothing Then Return 0
                
                Dim lCount As Integer = 1
                for each lChild in vNode.Children
                    lCount += CountNodes(lChild)
                Next
                
                Return lCount
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.CountNodes error: {ex.Message}")
                Return 0
            End Try
        End Function
        
    End Class
    
End Namespace