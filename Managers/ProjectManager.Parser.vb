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
        ''' Event raised when all files have been parsed
        ''' </summary>
        ''' <param name="vFileCount">Number of files parsed</param>
        ''' <param name="vTotalMilliseconds">Total time in milliseconds</param>
        Public Event AllFilesParseCompleted(vFileCount As Integer, vTotalMilliseconds As Double)
        
        
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
        ''' Parses all loaded source files asynchronously to populate LineMetadata
        ''' </summary>
        ''' <returns>Task that completes when all files are parsed</returns>
        ''' <remarks>
        ''' This method launches parallel tasks to parse all files efficiently.
        ''' Each file's LineMetadata arrays are populated, then colors are applied.
        ''' </remarks>
        Public Async Function ParseAllFilesAsync() As Task
            Try
                If pSourceFiles Is Nothing OrElse pSourceFiles.Count = 0 Then
                    'Console.WriteLine("ProjectManager.ParseAllFilesAsync: No source files to parse")
                    Return
                End If
                
               ' Console.WriteLine($"ProjectManager.ParseAllFilesAsync: Starting parallel parse of {pSourceFiles.Count} files")
                Dim lStartTime As DateTime = DateTime.Now
                
                ' Create list of parse tasks
                Dim lParseTasks As New List(Of Task)()
                Dim lFileCount As Integer = 0
                Dim lTotalFiles As Integer = pSourceFiles.Count
                Dim lSkippedFiles As Integer = 0
                
                ' Report initial progress
                Gtk.Application.Invoke(Sub()
                    RaiseEvent ParsingProgress(0, lTotalFiles, "Starting parse...")
                End Sub)
                
                ' Add small delay to ensure UI sees the initial progress
                Await Task.Delay(50)
                
                ' Launch a parse task for each file
                for each lKvp As KeyValuePair(Of String, SourceFileInfo) in pSourceFiles
                    Dim lSourceFile As SourceFileInfo = lKvp.Value
                    
                    ' Skip if already parsed and up to date
                    If lSourceFile.IsParsed AndAlso Not lSourceFile.NeedsParsing Then
                        'Console.WriteLine($"  Skipping {lSourceFile.FileName} - already parsed")
                        lSkippedFiles += 1
                        ' Still count it as progress
                        SyncLock pSourceFiles
                            lFileCount += 1
                        End SyncLock
                        Continue for
                    End If
                    
                    ' Create parse task for this file
                    Dim lTask As Task = Task.Run(Sub()
                        Try
                            Dim lFileStartTime As DateTime = DateTime.Now
                            
                            ' Parse the file (tokens only, no colors)
                            Dim lParseResult As ParseResult = ParseFileContent(lSourceFile)
                            
                            If lParseResult IsNot Nothing Then
                                ' Update syntax tree
                                lSourceFile.SyntaxTree = lParseResult.RootNode
                                
                                ' Store line metadata (no colors yet)
                                UpdateSourceFileMetadata(lSourceFile, lParseResult)
                                
                                ' Mark as parsed
                                lSourceFile.IsParsed = True
                                lSourceFile.NeedsParsing = False
                                lSourceFile.LastParsed = DateTime.Now
                                
                                Dim lFileElapsed As TimeSpan = DateTime.Now - lFileStartTime
                                'Console.WriteLine($"  Parsed {lSourceFile.FileName} in {lFileElapsed.TotalMilliseconds:F2}ms")
                            End If
                            
                            ' Update progress (thread-safe)
                            Dim lCurrentCount As Integer = 0
                            Dim lCurrentFileName As String = lSourceFile.FileName
                            
                            SyncLock pSourceFiles
                                lFileCount += 1
                                lCurrentCount = lFileCount
                            End SyncLock
                            
                            ' Report progress more frequently - every file for first 10, then less often
                            Dim lShouldReport As Boolean = False
                            If lCurrentCount <= 10 Then
                                lShouldReport = True  ' Report every file for first 10
                            ElseIf lCurrentCount <= 50 Then
                                lShouldReport = (lCurrentCount Mod 2 = 0)  ' Every 2nd file for next 40
                            ElseIf lCurrentCount <= 100 Then
                                lShouldReport = (lCurrentCount Mod 5 = 0)  ' Every 5th file for next 50
                            Else
                                lShouldReport = (lCurrentCount Mod 10 = 0)  ' Every 10th file after 100
                            End If
                            
                            ' Always report milestones
                            If lCurrentCount = 1 OrElse _
                               lCurrentCount = lTotalFiles \ 4 OrElse _
                               lCurrentCount = lTotalFiles \ 2 OrElse _
                               lCurrentCount = (lTotalFiles * 3) \ 4 OrElse _
                               lCurrentCount = lTotalFiles Then
                                lShouldReport = True
                            End If
                            
                            ' Report progress on UI thread if needed
                            If lShouldReport Then
                                Gtk.Application.Invoke(Sub()
                                    RaiseEvent ParsingProgress(lCurrentCount, lTotalFiles, lCurrentFileName)
                                End Sub)
                            End If
                            
                        Catch ex As Exception
                            'Console.WriteLine($"ParseAllFilesAsync task error for {lSourceFile.FileName}: {ex.Message}")
                        End Try
                    End Sub)
                    
                    lParseTasks.Add(lTask)
                Next
                
                ' Wait for all parsing to complete
                Await Task.WhenAll(lParseTasks)
                
                ' Small delay before theme application for visual effect
                Await Task.Delay(100)
                
                ' Report theme application start
                Gtk.Application.Invoke(Sub()
                    RaiseEvent ParsingProgress(lTotalFiles, lTotalFiles, "Applying theme colors...")
                End Sub)
                
                ' Small delay to show the message
                Await Task.Delay(50)
                
                ' After all parsing is done, apply theme colors to all files
                ' CRITICAL: This MUST run on the UI thread because it manipulates GTK CSS providers
                If pThemeManager IsNot Nothing Then
                    ' Use a TaskCompletionSource to await the UI thread operation
                    Dim lTcs As New TaskCompletionSource(Of Boolean)()
                    
                    Gtk.Application.Invoke(Sub()
                        Try
                            ApplyCurrentTheme()
                            ' Report completion at 100%
                            RaiseEvent ParsingProgress(lTotalFiles, lTotalFiles, "Theme application complete")
                            lTcs.SetResult(True)
                        Catch ex As Exception
                            Console.WriteLine($"ApplyCurrentTheme error: {ex.Message}")
                            lTcs.SetException(ex)
                        End Try
                    End Sub)
                    
                    ' Wait for the theme application to complete on UI thread
                    Await lTcs.Task
                End If
                
                Dim lElapsed As TimeSpan = DateTime.Now - lStartTime
                Dim lParsedCount As Integer = lTotalFiles - lSkippedFiles
                'Console.WriteLine($"ParseAllFilesAsync: Completed - parsed {lParsedCount} files, skipped {lSkippedFiles} cached files in {lElapsed.TotalSeconds:F2} seconds")
                
                ' Small delay before final completion for visual effect
                Await Task.Delay(100)
                
                ' Notify completion on UI thread
                Gtk.Application.Invoke(Sub()
                    RaiseEvent AllFilesParseCompleted(pSourceFiles.Count, lElapsed.TotalMilliseconds)
                End Sub)
                
            Catch ex As Exception
                Console.WriteLine($"ParseAllFilesAsync error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Function

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
                    for each lChild in pProjectSyntaxTree.Children
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
        
        ' Replace: SimpleIDE.Managers.ProjectManager.LoadProjectStructure
        ''' <summary>
        ''' Loads the project structure by parsing all source files with progress reporting
        ''' </summary>
        ''' <returns>True if successful, False otherwise</returns>
        Public Function LoadProjectStructure() As Boolean
            Try
                Console.WriteLine("ProjectManager: Starting project structure parse...")
                
                ' Ensure we have a project loaded
                If Not pIsProjectOpen OrElse pCurrentProjectInfo Is Nothing Then
                    Console.WriteLine("ProjectManager: No project is currently loaded")
                    Return False
                End If
                
                ' Ensure all source files are loaded first
                If Not LoadAllSourceFiles() Then
                    Console.WriteLine("ProjectManager: Failed to load source files")
                    Return False
                End If
                
                ' Initialize parser if needed
                InitializeParser()
                
                ' Get total file count for progress reporting
                Dim lTotalFiles As Integer = pSourceFiles.Count
                Console.WriteLine($"ProjectManager: Will parse {lTotalFiles} files")
                
                ' Report initial progress
                RaiseEvent ParsingProgress(0, lTotalFiles, "Initializing parser...")
                
                ' Parse the project - but we need to add progress reporting
                ' Since ProjectParser.ParseProject doesn't report progress, we'll simulate it
                Dim lFileIndex As Integer = 0
                
                ' Report progress for each file (simulate based on file count)
                ' We'll report progress in chunks as a workaround
                Dim lProgressTimer As System.Threading.Timer = Nothing
                Dim lParseComplete As Boolean = False
                
                ' Create a timer to report simulated progress
                lProgressTimer = New System.Threading.Timer(
                    Sub(state)
                        If Not lParseComplete AndAlso lFileIndex < lTotalFiles Then
                            lFileIndex += Math.Max(1, lTotalFiles \ 20) ' Update in ~20 steps
                            If lFileIndex > lTotalFiles Then lFileIndex = lTotalFiles
                            
                            Dim lFileName As String = ""
                            If pSourceFiles.Count > lFileIndex Then
                                Dim lFiles As List(Of String) = pSourceFiles.Keys.ToList()
                                If lFileIndex < lFiles.Count Then
                                    lFileName = Path.GetFileName(lFiles(lFileIndex))
                                End If
                            End If
                            
                            ' Raise progress event on UI thread
                            Gtk.Application.Invoke(Sub()
                                RaiseEvent ParsingProgress(lFileIndex, lTotalFiles, lFileName)
                            End Sub)
                        End If
                    End Sub,
                    Nothing, 100, 100) ' Update every 100ms
                
                ' Do the actual parsing
                pProjectSyntaxTree = pProjectParser.ParseProject()
                lParseComplete = True
                
                ' Stop the progress timer
                If lProgressTimer IsNot Nothing Then
                    lProgressTimer.Dispose()
                End If
                
                ' Report completion
                RaiseEvent ParsingProgress(lTotalFiles, lTotalFiles, "Complete")
                
                If pProjectParser IsNot Nothing Then
                    pParseErrors = pProjectParser.GetParseErrors()
                End If
                
                pLastParseTime = DateTime.Now
                
                ' Check if we actually got a valid tree
                If pProjectSyntaxTree IsNot Nothing Then
                    Console.WriteLine($"ProjectManager: ParseProject returned tree with root: {pProjectSyntaxTree.Name}")
                    Console.WriteLine($"  Root type: {pProjectSyntaxTree.NodeType}")
                    Console.WriteLine($"  Children count: {pProjectSyntaxTree.Children.Count}")
                Else
                    Console.WriteLine("ProjectManager: ParseProject returned Nothing!")
                End If
                
                ' Raise completion events
                If pProjectSyntaxTree IsNot Nothing Then
                    RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                    Console.WriteLine("ProjectManager: Raised ProjectStructureLoaded event")
                End If
                
                If pParseErrors IsNot Nothing AndAlso pParseErrors.Count > 0 Then
                    RaiseEvent ParseErrorsOccurred(pParseErrors)
                End If
                
                ' Trigger async parsing for colors
                Task.Run(Function() ParseAllFilesAsync())
                
                Return pProjectSyntaxTree IsNot Nothing
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProjectStructure error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Helper method to parse project with progress reporting
        ''' </summary>
        Private Function ParseProjectWithProgress(vTotalFiles As Integer) As SyntaxNode
            Try
                ' We need to intercept the ProjectParser's file processing
                ' Since we can't easily modify ProjectParser, we'll report progress based on files processed
                
                Dim lFileCount As Integer = 0
                Dim lSortedFiles As New List(Of KeyValuePair(Of String, SourceFileInfo))(pSourceFiles)
                lSortedFiles.Sort(Function(a, b) String.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase))
                
                ' Report progress for each file
                for each lFileEntry in lSortedFiles
                    lFileCount += 1
                    Dim lFileName As String = Path.GetFileName(lFileEntry.Key)
                    
                    ' Report progress BEFORE parsing each file
                    RaiseEvent ParsingProgress(lFileCount, vTotalFiles, lFileName)
                    
                    ' Allow UI to update
                    System.Threading.Thread.Sleep(1) ' Brief yield to allow UI thread to process
                Next
                
                ' Now do the actual parsing
                Return pProjectParser.ParseProject()
                
            Catch ex As Exception
                Console.WriteLine($"ParseProjectWithProgress error: {ex.Message}")
                Return Nothing
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
        ''' Loads all source files in the project and triggers async parsing
        ''' </summary>
        ''' <returns>True if all files loaded successfully</returns>
        ''' <remarks>
        ''' After loading all files, this method triggers ParseAllFilesAsync to
        ''' pre-compute LineMetadata and CharacterColors for instant rendering
        ''' </remarks>
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
                for each lFilePath in lSourceFilePaths
                    Try
                        ' Check if already loaded
                        Dim lSourceFile As SourceFileInfo = Nothing
                        
                        If pSourceFiles.ContainsKey(lFilePath) Then
                            lSourceFile = pSourceFiles(lFilePath)
                            If lSourceFile.IsLoaded Then
                                lSuccessCount += 1
                                Continue for
                            End If
                        Else
                            ' Create new SourceFileInfo
                            lSourceFile = New SourceFileInfo(lFilePath, "", pCurrentProjectInfo.ProjectDirectory)
                            lSourceFile.ProjectRootNamespace = pCurrentProjectInfo.GetEffectiveRootNamespace()
                            
                            ' Wire up the ProjectManagerRequested event
                            AddHandler lSourceFile.ProjectManagerRequested, AddressOf OnSourceFileInfoRequestProjectManager
                            
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
                    for each lPath in lFailedFiles.Take(5)
                        Console.WriteLine($"  - {Path.GetFileName(lPath)}")
                    Next
                End If
                
                ' CRITICAL: Trigger async parsing of all loaded files
                If lSuccessCount > 0 Then
                    Console.WriteLine("ProjectManager: Triggering async parse of all loaded files...")
                    
                    ' Launch the async parsing - don't await here to avoid blocking
                    Task.Run(Function() ParseAllFilesAsync())
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

        ' Replace: SimpleIDE.Managers.ProjectManager.ParseFileAsync
        ''' <summary>
        ''' Parses a file asynchronously and updates both LineMetadata and CharacterColors
        ''' </summary>
        ''' <param name="vFile">The source file to parse</param>
        ''' <remarks>
        ''' This method performs two critical steps:
        ''' 1. Parse the file to get LineMetadata with syntax tokens
        ''' 2. Apply theme colors to generate CharacterColors array
        ''' </remarks>
        Public Async Function ParseFileAsync(vFile As SourceFileInfo) As Task
            Try
                If vFile Is Nothing Then Return
                
                'Console.WriteLine($"ProjectManager.ParseFileAsync: Starting async parse for {vFile.FileName}")
                
                ' Run parsing in background thread
                Await Task.Run(Sub()
                    Try
                        ' Ensure Parser is initialized
                        If Parser Is Nothing Then
                            Console.WriteLine("ProjectManager.ParseFileAsync: Parser not initialized")
                            Return
                        End If
                        
                        ' Parse the file content
                        Dim lStartTime As DateTime = DateTime.Now
                        Dim lParseResult As ParseResult = ParseFileContent(vFile)
                        Dim lElapsed As TimeSpan = DateTime.Now - lStartTime
                        
                        'Console.WriteLine($"ProjectManager.ParseFileAsync: Parse completed in {lElapsed.TotalMilliseconds:F2}ms")
                        
                        ' Update SourceFileInfo on the background thread
                        If lParseResult IsNot Nothing Then
                            ' Update syntax tree
                            vFile.SyntaxTree = lParseResult.RootNode
                            
                            ' Update LineMetadata ONLY (not CharacterColors)
                            UpdateSourceFileMetadata(vFile, lParseResult)
                            
                            ' Update parse state
                            vFile.IsParsed = True
                            vFile.NeedsParsing = False
                            vFile.LastParsed = DateTime.Now
                            
                            ' Copy any parse errors
                            If lParseResult.Errors IsNot Nothing Then
                                vFile.ParseErrors = lParseResult.Errors
                            End If
                            
                            ' CRITICAL FIX: Apply theme colors immediately after parsing
                            'Console.WriteLine($"ProjectManager.ParseFileAsync: Applying theme colors for {vFile.FileName}")
                            
                            ' Check if we have tokens to colorize
                            Dim lHasTokens As Boolean = False
                            If vFile.LineMetadata IsNot Nothing Then
                                for each lMetadata in vFile.LineMetadata
                                    If lMetadata?.SyntaxTokens IsNot Nothing AndAlso lMetadata.SyntaxTokens.Count > 0 Then
                                        lHasTokens = True
                                        Exit for
                                    End If
                                Next
                            End If
                            
                            If lHasTokens Then
                               ' Console.WriteLine($"ProjectManager.ParseFileAsync: Found syntax tokens, applying colors...")
                                UpdateFileColorsFromTheme(vFile)
                                'Console.WriteLine($"ProjectManager.ParseFileAsync: CharacterColors updated successfully for {vFile.FileName}")
                            Else
                                Console.WriteLine($"ProjectManager.ParseFileAsync: No syntax tokens found for {vFile.FileName}")
                            End If
                        Else
                            Console.WriteLine($"ProjectManager.ParseFileAsync: Parse result was Nothing for {vFile.FileName}")
                        End If
                        
                        ' Notify listeners on UI thread
                        Gtk.Application.Invoke(Sub()
                            Try
                                ' Raise parse completed event
                                RaiseEvent ParseCompleted(vFile, vFile.SyntaxTree)
                                
                                ' Notify the SourceFileInfo that rendering has changed
                                vFile.NotifyRenderingChanged(0, vFile.TextLines.Count - 1)
                                
                                Console.WriteLine($"ProjectManager.ParseFileAsync: Notified UI and triggered redraw for {vFile.FileName}")
                                
                            Catch ex As Exception
                                Console.WriteLine($"ParseFileAsync UI notification error: {ex.Message}")
                                Console.WriteLine($"  Stack: {ex.StackTrace}")
                            End Try
                        End Sub)
                        
                    Catch ex As Exception
                        Console.WriteLine($"ParseFileAsync background task error: {ex.Message}")
                        Console.WriteLine($"  Stack: {ex.StackTrace}")
                    End Try
                End Sub)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.ParseFileAsync error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Function

        ''' <summary>
        ''' Updates only the LineMetadata array in SourceFileInfo based on parse results
        ''' </summary>
        ''' <param name="vFile">The source file to update</param>
        ''' <param name="vParseResult">The parse result containing tokens</param>
        ''' <remarks>
        ''' This method updates ONLY the LineMetadata array with token information.
        ''' CharacterColors are updated separately by UpdateFileColorsFromTheme.
        ''' </remarks>
        Private Sub UpdateSourceFileMetadata(vFile As SourceFileInfo, vParseResult As Syntax.ParseResult)
            Try
                If vFile Is Nothing OrElse vParseResult Is Nothing Then Return
                If vParseResult.LineMetadata Is Nothing Then Return
                
                Dim lLineCount As Integer = vFile.TextLines.Count
                
                
                ' Copy LineMetadata from parse result
                for i As Integer = 0 To Math.Min(lLineCount - 1, vParseResult.LineMetadata.Length - 1)
                    vFile.LineMetadata(i) = vParseResult.LineMetadata(i)
                Next
                
                'Console.WriteLine($"UpdateSourceFileMetadata: Updated {lLineCount} lines of metadata for {vFile.FileName}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateSourceFileMetadata error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates the CharacterColors array for a file based on current theme
        ''' </summary>
        ''' <param name="vFile">The source file to update colors for</param>
        ''' <remarks>
        ''' This is called after parsing to apply theme colors to the already-parsed tokens
        ''' </remarks>
        Private Sub UpdateFileColorsFromTheme(vFile As SourceFileInfo)
            Try
                If vFile Is Nothing OrElse vFile.LineMetadata Is Nothing Then 
                    'Console.WriteLine($"UpdateFileColorsFromTheme: File or LineMetadata is Nothing")
                    Return
                End If
                
                If pThemeManager Is Nothing Then 
                   ' Console.WriteLine($"UpdateFileColorsFromTheme: ThemeManager is Nothing")
                    Return
                End If
                
                Dim lTheme As EditorTheme = pThemeManager.GetCurrentThemeObject()
                If lTheme Is Nothing Then 
                    'Console.WriteLine($"UpdateFileColorsFromTheme: Current theme is Nothing")
                    Return
                End If
                
                'Console.WriteLine($"UpdateFileColorsFromTheme: Starting color update for {vFile.FileName}")
                
                ' Get default color from theme
                Dim lDefaultColor As String = lTheme.ForegroundColor
                If String.IsNullOrEmpty(lDefaultColor) Then
                    lDefaultColor = "#D4D4D4"  ' Fallback default
                    'Console.WriteLine($"UpdateFileColorsFromTheme: Using fallback default color")
                End If
                
                ' Create color map from current theme
                Dim lColorMap As Dictionary(Of SyntaxTokenType, String) = CreateThemeColorMap(lTheme)
                'Console.WriteLine($"UpdateFileColorsFromTheme: Color map created with {lColorMap.Count} entries")
                
                ' Ensure CharacterColors array is properly sized
                vFile.EnsureCharacterColors()
                
                ' Update colors for each line using the SourceFileInfo's method
                Dim lLineCount As Integer = vFile.TextLines.Count
                Dim lLinesWithTokens As Integer = 0
                
                for i As Integer = 0 To lLineCount - 1
                    ' Get the metadata for this line
                    Dim lMetadata As LineMetadata = vFile.GetLineMetadata(i)
                    
                    If lMetadata IsNot Nothing AndAlso lMetadata.SyntaxTokens IsNot Nothing AndAlso lMetadata.SyntaxTokens.Count > 0 Then
                        ' Call the SourceFileInfo's UpdateCharacterColors method for this line
                        vFile.UpdateCharacterColors(i, lMetadata.SyntaxTokens, lColorMap)
                        lLinesWithTokens += 1
                    Else
                        ' No tokens for this line, ensure it has default colors
                        If i < vFile.CharacterColors.Length Then
                            Dim lLineLength As Integer = vFile.TextLines(i).Length
                            If lLineLength > 0 Then
                                If vFile.CharacterColors(i) Is Nothing OrElse vFile.CharacterColors(i).Length <> lLineLength Then
                                    ReDim vFile.CharacterColors(i)(lLineLength - 1)
                                End If
                                for j As Integer = 0 To lLineLength - 1
                                    vFile.CharacterColors(i)(j) = New CharacterColorInfo(lDefaultColor)
                                Next
                            Else
                                vFile.CharacterColors(i) = New CharacterColorInfo() {}
                            End If
                        End If
                    End If
                Next
                
                'Console.WriteLine($"UpdateFileColorsFromTheme: Updated {lLinesWithTokens} lines with tokens for {vFile.FileName}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFileColorsFromTheme error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub


        ''' <summary>
        ''' Parses file content and returns a ParseResult with token information only
        ''' </summary>
        ''' <param name="vFile">The source file to parse</param>
        ''' <returns>ParseResult containing tokens and metadata (no colors)</returns>
        Private Function ParseFileContent(vFile As SourceFileInfo) As Syntax.ParseResult
            Try
                Dim lResult As New ParseResult()
                lResult.FilePath = vFile.FilePath
                
                ' Initialize LineMetadata array
                Dim lLineCount As Integer = vFile.TextLines.Count
                ReDim lResult.LineMetadata(Math.Max(0, lLineCount - 1))
                
                ' Parse each line
                for i As Integer = 0 To lLineCount - 1
                    Dim lLineText As String = vFile.TextLines(i)
                    Dim lMetadata As New LineMetadata()
                    
                    ' Tokenize the line (without colors)
                    Dim lTokens As List(Of SyntaxToken) = TokenizeLine(lLineText, i)
                    lMetadata.SyntaxTokens = lTokens
                    lMetadata.UpdateHash(lLineText)
                    lMetadata.ParseState = LineParseState.eParsed
                    
                    ' Store in result
                    lResult.LineMetadata(i) = lMetadata
                Next
                
                ' Parse overall structure for SyntaxNode tree
                If Parser IsNot Nothing Then
                    Dim lFullResult As Object = Parser.ParseContent(vFile.Content, RootNamespace, vFile.FilePath)
                    If TypeOf lFullResult Is ParseResult Then
                        Dim lFullParseResult As ParseResult = DirectCast(lFullResult, ParseResult)
                        lResult.RootNode = lFullParseResult.RootNode
                        lResult.Errors = lFullParseResult.Errors
                    End If
                End If
                
                'Console.WriteLine($"ParseFileContent: Parsed {lLineCount} lines for {vFile.FileName}")
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"ParseFileContent error: {ex.Message}")
                Return New ParseResult()
            End Try
        End Function

        
        ''' <summary>
        ''' Tokenizes a single line of VB.NET code without color information
        ''' </summary>
        ''' <param name="vLineText">The line text to tokenize</param>
        ''' <param name="vLineIndex">The line index for positioning</param>
        ''' <returns>List of syntax tokens (without colors)</returns>
        Private Function TokenizeLine(vLineText As String, vLineIndex As Integer) As List(Of SyntaxToken)
            Try
                Dim lTokens As New List(Of SyntaxToken)()
                
                If String.IsNullOrEmpty(vLineText) Then Return lTokens
                
                ' Use VBTokenizer if available
                Dim lTokenizer As New VBTokenizer()
                Dim lRawTokens As List(Of Token) = lTokenizer.TokenizeLine(vLineText)
                
                ' Convert Token to SyntaxToken WITHOUT colors
                for each lRawToken in lRawTokens
                    Dim lSyntaxType As SyntaxTokenType = MapTokenTypeToSyntaxType(lRawToken.Type)
                    
                    ' Create SyntaxToken without color (Color property removed from class)
                    Dim lSyntaxToken As New SyntaxToken(
                        lRawToken.StartColumn,
                        lRawToken.EndColumn - lRawToken.StartColumn + 1,
                        lSyntaxType
                    )
                    
                    lTokens.Add(lSyntaxToken)
                Next
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"TokenizeLine error: {ex.Message}")
                Return New List(Of SyntaxToken)()
            End Try
        End Function
        
        ''' <summary>
        ''' Maps a Token type to a SyntaxToken type
        ''' </summary>
        Private Function MapTokenTypeToSyntaxType(vTokenType As TokenType) As SyntaxTokenType
            Select Case vTokenType
                Case TokenType.eKeyword
                    Return SyntaxTokenType.eKeyword
                Case TokenType.eIdentifier
                    Return SyntaxTokenType.eIdentifier
                Case TokenType.eStringLiteral
                    Return SyntaxTokenType.eString
                Case TokenType.eNumber
                    Return SyntaxTokenType.eNumber
                Case TokenType.eComment
                    Return SyntaxTokenType.eComment
                Case TokenType.eOperator
                    Return SyntaxTokenType.eOperator
                Case TokenType.eType
                    Return SyntaxTokenType.eType
                Case Else
                    Return SyntaxTokenType.eNormal
            End Select
        End Function
        
        ''' <summary>
        ''' Updates the CharacterColors array for a file based on LineMetadata and color map
        ''' </summary>
        ''' <param name="vFile">The source file to update</param>
        ''' <param name="vColorMap">Dictionary mapping token types to colors</param>
        ''' <param name="vDefaultColor">Default color for unspecified tokens</param>
        ''' <remarks>
        ''' This method applies theme colors to already-parsed tokens in LineMetadata
        ''' </remarks>
        Private Sub UpdateFileCharacterColors(vFile As SourceFileInfo, vColorMap As Dictionary(Of SyntaxTokenType, String), vDefaultColor As String)
            Try
                If vFile Is Nothing OrElse vFile.LineMetadata Is Nothing Then
                    Console.WriteLine($"UpdateFileCharacterColors: File or LineMetadata is Nothing")
                    Return
                End If
                
                If vColorMap Is Nothing Then
                    Console.WriteLine($"UpdateFileCharacterColors: ColorMap is Nothing")
                    Return
                End If
                
                Dim lLineCount As Integer = vFile.TextLines.Count
                Console.WriteLine($"UpdateFileCharacterColors: Processing {lLineCount} lines for {vFile.FileName}")
                
                ' Ensure CharacterColors array is properly sized
                vFile.EnsureCharacterColors()
                
                ' Process each line
                for i As Integer = 0 To lLineCount - 1
                    Dim lLineText As String = vFile.TextLines(i)
                    Dim lLineLength As Integer = lLineText.Length
                    Dim lMetadata As LineMetadata = vFile.GetLineMetadata(i)
                    
                    ' Ensure the CharacterColors array for this line is properly sized
                    If vFile.CharacterColors(i) Is Nothing OrElse vFile.CharacterColors(i).Length <> lLineLength Then
                        If lLineLength > 0 Then
                            ReDim vFile.CharacterColors(i)(lLineLength - 1)
                        Else
                            vFile.CharacterColors(i) = New CharacterColorInfo() {}
                            Continue for
                        End If
                    End If
                    
                    ' First, set all characters to default color
                    for j As Integer = 0 To lLineLength - 1
                        vFile.CharacterColors(i)(j) = New CharacterColorInfo(vDefaultColor)
                    Next
                    
                    ' Apply colors based on syntax tokens in metadata
                    If lMetadata IsNot Nothing AndAlso lMetadata.SyntaxTokens IsNot Nothing Then
                        for each lToken in lMetadata.SyntaxTokens
                            ' Get the color for this token type
                            Dim lColor As String = vDefaultColor
                            If vColorMap.ContainsKey(lToken.TokenType) Then
                                lColor = vColorMap(lToken.TokenType)
                            End If
                            
                            ' Apply color to the character range
                            ' Ensure we don't go out of bounds
                            Dim lStartCol As Integer = Math.Max(0, lToken.StartColumn)
                            Dim lEndCol As Integer = Math.Min(lLineLength - 1, lToken.StartColumn + lToken.Length - 1)
                            
                            for k As Integer = lStartCol To lEndCol
                                If k >= 0 AndAlso k < vFile.CharacterColors(i).Length Then
                                    vFile.CharacterColors(i)(k) = New CharacterColorInfo(lColor)
                                End If
                            Next
                        Next
                    End If
                Next
                
                Console.WriteLine($"UpdateFileCharacterColors: Completed color update for {vFile.FileName}")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFileCharacterColors error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub

        
    End Class
    
End Namespace