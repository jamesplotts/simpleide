' Models/SourceFileInfo.vb - File parsing and structure information
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Models
    
    ''' <summary>
    ''' Represents a source file with its parsed structure
    ''' </summary>
    Public Class SourceFileInfo
        ' ===== Properties =====
        Public Property FilePath As String
        Public Property FileName As String
        Public Property ProjectDirectory As String
        Public Property Content As String
        Public Property SyntaxTree As SyntaxNode
        Public Property ParseErrors As List(Of ParseError)
        Public Property LastParsed As DateTime
        Public Property IsLoaded As Boolean = False
        Public Property IsParsed As Boolean = False
        Public Property ProjectRootNamespace As String = ""
        Private Property pTextLines As List(Of String) = New List(Of String)
        Private pLineMetadata As LineMetadata()
        Public Property RelativePath As String = ""  
        Public Property NeedsParsing As Boolean = True
        Private pIdentifierCaseMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)  
        Private pKeywordCaseMap As Dictionary(Of String, String) = Nothing
        Private pFileEncoding As Encoding
        Private pProjectManager As ProjectManager

        Private pCharacterTokens As Byte()()

        ''' <summary>
        ''' Gets or sets the character token array for syntax highlighting
        ''' </summary>
        ''' <value>Array of byte arrays, one per line, each byte encoding token type and style</value>
        Public Property CharacterTokens As Byte()()
            Get
                Return pCharacterTokens
            End Get
            Set(value As Byte()())
                pCharacterTokens = value
            End Set
        End Property

        ''' <summary>
        ''' Raised when the file content changes
        ''' </summary>
        Public Event ContentChanged As EventHandler
        
        ''' <summary>
        ''' Gets or sets the last modification timestamp
        ''' </summary>
        Public Property LastModified As DateTime = DateTime.Now
        
        ''' <summary>
        ''' Gets or sets whether the file has been modified
        ''' </summary>
        Public Property IsModified As Boolean = False
        
        ''' <summary>
        ''' Gets or sets the parse result from the parsing engine
        ''' </summary>
        Public Property ParseResult As SyntaxNode

        ''' <summary>
        ''' Gets or sets the text lines collection
        ''' </summary>
        ''' <value>List of text lines in the file</value>
        Public Property TextLines As List(Of String)
            Get
                ' Ensure pTextLines is initialized
                If pTextLines Is Nothing Then
                    pTextLines = New List(Of String)()
                End If
                ' Ensure at least one line exists
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                Return pTextLines
            End Get
            Set(value As List(Of String))
                pTextLines = value
                ' Ensure we always have at least one line
                If pTextLines IsNot Nothing AndAlso pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
            End Set
        End Property

        ''' <summary>
        ''' Gets the line metadata array for syntax highlighting and structure
        ''' </summary>
        ''' <value>Array of LineMetadata objects, one per line</value>
        Public ReadOnly Property LineMetadata As LineMetadata()
            Get
                ' OPTIMIZATION: Don't call EnsureLineMetadata here - it should already be initialized
                ' at the appropriate lifecycle points (LoadContent, constructors, line operations)
                
                ' If it's truly not initialized, return an empty array rather than initializing
                ' This makes the property getter fast and predictable
                If pLineMetadata Is Nothing Then
                    ' This should rarely happen if lifecycle methods are correct
                    ' Return empty array to prevent null reference exceptions
                    Return New LineMetadata() {}
                End If
                
                Return pLineMetadata
            End Get
        End Property
  
        ' Demo Mode is used when you want to display a fictional file's content without having any file IO.
        Public IsDemoMode As Boolean = False

        ''' <summary>
        ''' Event raised to request a reference to the ProjectManager
        ''' </summary>
        Public Event ProjectManagerRequested As EventHandler(Of ProjectManagerRequestEventArgs)

        ''' <summary>
        ''' Event raised when rendering needs to be updated
        ''' </summary>
        ''' <remarks>
        ''' This event is raised when the character tokens or syntax highlighting
        ''' changes and the editor needs to redraw the affected lines
        ''' </remarks>
        Public Event RenderingChanged As EventHandler
        
        ' ===== Constructor =====

        ''' <summary>
        ''' Initializes a new instance with flexible mode detection
        ''' </summary>
        ''' <param name="vFilePath">Full path to the source file (or display name for virtual files)</param>
        ''' <param name="vContent">Optional initial content (empty string means load from disk if file exists)</param>
        ''' <param name="vProjectDirectory">Project directory for calculating relative paths</param>
        ''' <remarks>
        ''' Mode detection logic:
        ''' - Empty filepath with empty content: Demo/virtual mode (for UI components)
        ''' - Virtual file paths (ai-artifact:, comparison:, etc.): Demo mode
        ''' - File exists on disk with empty content: Regular file mode (load from disk)
        ''' - File doesn't exist with content provided: Demo/virtual mode
        ''' - File doesn't exist with no content: New file mode (will be created)
        ''' </remarks>
        Public Sub New(vFilePath As String, vContent As String, vProjectDirectory As String)
            Try
                ' Determine if this is demo/virtual mode or regular file loading
                Dim lIsDemoMode As Boolean = False
                Dim lIsVirtualFile As Boolean = False
                
                ' Special case: Empty filepath indicates virtual/temporary file
                If String.IsNullOrEmpty(vFilePath) Then
                    lIsDemoMode = True
                    lIsVirtualFile = True
                    vFilePath = "Untitled"  ' Give it a default name
                ' Check for virtual file indicators (AI artifacts, comparisons, etc.)
                ElseIf vFilePath.Contains("ai-artifact:") OrElse _
                       vFilePath.Contains("comparison:") OrElse _
                       vFilePath.Contains("Demo") OrElse _
                       vFilePath.Contains("Theme") OrElse _
                       vFilePath.Contains("Untitled") Then
                    lIsVirtualFile = True
                    lIsDemoMode = True
                ' Determine mode based on file existence and content
                ElseIf Not String.IsNullOrEmpty(vContent) Then
                    ' Content was explicitly provided
                    If Not File.Exists(vFilePath) Then
                        ' File doesn't exist but content provided - demo/virtual mode
                        lIsDemoMode = True
                    End If
                ElseIf String.IsNullOrEmpty(vContent) AndAlso Not File.Exists(vFilePath) Then
                    ' No content and file doesn't exist - this is for a new file that will be created
                    lIsDemoMode = False  ' Will be loaded/created later
                End If
                
                ' Set basic properties
                FilePath = vFilePath
                FileName = If(lIsVirtualFile, vFilePath, Path.GetFileName(vFilePath))
                ProjectDirectory = vProjectDirectory
                IsDemoMode = lIsDemoMode
                
                ' Calculate relative path
                If Not lIsVirtualFile AndAlso Not String.IsNullOrEmpty(vProjectDirectory) AndAlso _
                   Not String.IsNullOrEmpty(vFilePath) AndAlso vFilePath.StartsWith(vProjectDirectory) Then
                    RelativePath = vFilePath.Substring(vProjectDirectory.Length).TrimStart(Path.DirectorySeparatorChar)
                Else
                    RelativePath = FileName
                End If
                
                ' Initialize collections
                ParseErrors = New List(Of ParseError)()
                
                If lIsDemoMode OrElse Not String.IsNullOrEmpty(vContent) Then
                    ' Demo/virtual mode or content explicitly provided - use the provided content
                    Content = If(vContent, "")
                    'Console.WriteLine($"SourceFileInfo created with PROVIDED content for: {FileName} (Demo={lIsDemoMode}, Virtual={lIsVirtualFile})")
                Else
                    ' Regular mode - content will be loaded from disk
                    Content = ""  ' Will be loaded by LoadContent()
                    'Console.WriteLine($"SourceFileInfo created for DISK LOADING: {FilePath}")
                End If
                
                ' Split content into lines (even if empty)
                Dim lLines As String() = Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                pTextLines = New List(Of String)(lLines)
                
                ' Ensure at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
                ' Update text lines with case correction if not demo mode and not virtual
                If Not lIsDemoMode AndAlso Not lIsVirtualFile Then
                    for i As Integer = 0 To pTextLines.Count - 1
                        UpdateTextLineWithCaseCorrection(i)
                    Next
                End If
                
                ' Initialize all data arrays with proper default values
                InitializeDataArrays()
                
                ' Set state flags based on mode
                If lIsDemoMode OrElse Not String.IsNullOrEmpty(vContent) Then
                    IsLoaded = True  ' Content is immediately available
                Else
                    IsLoaded = False  ' Regular files need LoadContent() to be called
                End If
                
                IsParsed = False
                IsModified = False
                NeedsParsing = True
                LastParsed = DateTime.MinValue
                LastModified = DateTime.Now
                
                'Console.WriteLine($"  TextLines: {pTextLines.Count} lines")
                'Console.WriteLine($"  LineMetadata: {If(pLineMetadata?.Length, 0)} entries")
                'Console.WriteLine($"  CharacterColors: {If(pCharacterColors?.Length, 0)} lines")
               ' Console.WriteLine($"  IsLoaded: {IsLoaded}, IsDemoMode: {IsDemoMode}")
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo constructor error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                
                ' Ensure we have at least minimal valid state
                If pTextLines Is Nothing Then pTextLines = New List(Of String)({""})
                If pLineMetadata Is Nothing Then 
                    ReDim pLineMetadata(0)
                    pLineMetadata(0) = New LineMetadata()
                End If
            End Try
        End Sub

        ''' <summary>
        ''' Initializes a new instance for demo mode (used by ThemeEditor)
        ''' </summary>
        ''' <param name="vContent">Demo content to display</param>
        ''' <remarks>
        ''' Properly initializes all three arrays for immediate rendering
        ''' </remarks>
        Public Sub New(vContent As String)
            Try
                ' Set demo mode flag
                IsDemoMode = True
                Content = vContent
                
                ' Set default file info for demo
                FileName = "DemoFile.vb"
                FilePath = "DemoFile.vb"
                ProjectDirectory = ""
                RelativePath = "DemoFile.vb"
                
                ' Initialize collections
                ParseErrors = New List(Of ParseError)()
                
                ' Split content into lines
                Dim lLines As String() = Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                pTextLines = New List(Of String)(lLines)
                
                ' Ensure at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If

                for I As Integer = 0 To pTextLines.Count - 1
                    UpdateTextLineWithCaseCorrection(I)
                Next
                
                ' Initialize all data arrays with proper default values
                InitializeDataArrays()
                
                ' Set state flags
                IsLoaded = True
                IsParsed = False
                IsModified = False
                NeedsParsing = True
                LastParsed = DateTime.MinValue
                LastModified = DateTime.Now
                
                'Console.WriteLine($"SourceFileInfo created in demo mode with {pTextLines.Count} lines")
               ' Console.WriteLine($"  LineMetadata initialized: {pLineMetadata.Length} entries")
                'Console.WriteLine($"  CharacterColors initialized: {pCharacterColors.Length} lines")
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo demo constructor error: {ex.Message}")
                ' Ensure we have at least minimal valid state
                If pTextLines Is Nothing Then pTextLines = New List(Of String)({"" })
                If pLineMetadata Is Nothing Then 
                    ReDim pLineMetadata(0)
                    pLineMetadata(0) = New LineMetadata()
                End If
            End Try
        End Sub


        
        ' ===== Public Methods =====
        
        ' Replace: SimpleIDE.Models.SourceFileInfo.LoadContent
        ''' <summary>
        ''' Loads the file content from disk and initializes data structures
        ''' </summary>
        ''' <returns>True if successful, False otherwise</returns>
        ''' <remarks>
        ''' This method loads the file content and requests async parsing from ProjectManager
        ''' </remarks>
        Public Function LoadContent() As Boolean
            Try
                ' Check if file exists
                If Not System.IO.File.Exists(FilePath) Then
                    'Console.WriteLine($"SourceFileInfo.LoadContent: File not found - {FilePath}")
                    ' Initialize with empty content for new files
                    Content = ""
                    IsLoaded = True
                    IsModified = True  ' New files are considered modified
                    Return True
                End If
                
                ' Read file content with encoding detection
                Dim lBytes As Byte() = System.IO.File.ReadAllBytes(FilePath)
                Dim lEncoding As Encoding = DetectEncoding(lBytes)
                pFileEncoding = lEncoding
                
                ' Convert to string using detected encoding
                Content = lEncoding.GetString(lBytes)
                
                ' Mark as loaded
                IsLoaded = True
                IsModified = False
                
                ' Split into lines
                UpdateTextLines()
                
                ' Ensure arrays are properly initialized
                EnsureLineMetadata()
                
                'Console.WriteLine($"SourceFileInfo.LoadContent: Loaded {FileName}")
                'Console.WriteLine($"  Lines: {pTextLines.Count}")
                'Console.WriteLine($"  Size: {Content.Length} bytes")
                'Console.WriteLine($"  LineMetadata array size: {pLineMetadata.Length}")
                'Console.WriteLine($"  CharacterColors array size: {pCharacterColors.Length}")
                
                ' CRITICAL FIX: Always request async parse after loading
                ' This ensures syntax highlighting is applied to newly loaded files
                NeedsParsing = True
                RequestAsyncParse()
                'Console.WriteLine($"SourceFileInfo.LoadContent: Requested async parse for {FileName}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.LoadContent error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Count nodes in the syntax tree
        ''' </summary>
        Private Function CountNodes(vNode As SyntaxNode) As Integer
            If vNode Is Nothing Then Return 0
            
            Dim lCount As Integer = 1
            If vNode.Children IsNot Nothing Then
                for each lChild in vNode.Children
                    lCount += CountNodes(lChild)
                Next
            End If
            Return lCount
        End Function
        
        ''' <summary>
        ''' Ensure the file is loaded
        ''' </summary>
        Public Function EnsureLoaded() As Boolean
            Try
                If IsLoaded Then Return True
                Return LoadContent()
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.EnsureLoaded error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parse the file (loads content if needed, then parses)
        ''' </summary>
        ''' <returns>True if successfully parsed, False otherwise</returns>
        ''' <remarks>
        ''' This method ensures content is loaded, then delegates to ProjectManager.ParseFile()
        ''' for centralized parsing with proper project context.
        ''' </remarks>
        <Obsolete("Use ProjectManager.ParseFile(sourceFileInfo) instead", False)>
        Public Function ParseFile() As Boolean
            Try
                If IsDemoMode Then Return ParseContent()
                
                ' Ensure content is loaded
                If Not IsLoaded Then
                    If Not LoadContent() Then
                        Console.WriteLine($"Failed to load content for {FileName}")
                        Return False
                    End If
                End If
                
                ' Request ProjectManager reference via event
                Dim lEventArgs As New ProjectManagerRequestEventArgs()
                RaiseEvent ProjectManagerRequested(Me, lEventArgs)
                
                If lEventArgs.HasProjectManager Then
                    ' Delegate to ProjectManager for centralized parsing
                    Console.WriteLine($"SourceFileInfo.ParseFile: Delegating to ProjectManager for {FileName}")
                    Return lEventArgs.ProjectManager.ParseFile(Me)
                Else
                    Console.WriteLine($"SourceFileInfo.ParseFile: No ProjectManager available for {FileName}")
                    Console.WriteLine($"  Parsing requires ProjectManager for proper context")
                    Return False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ParseFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Load and parse the file in one operation
        ''' </summary>
        ''' <returns>True if successfully loaded and parsed, False otherwise</returns>
        Public Function LoadAndParse() As Boolean
            Try
                ' Load content first
                If Not LoadContent() Then 
                    Console.WriteLine($"LoadAndParse: Failed to load content for {FileName}")
                    Return False
                End If
                
                ' Request ProjectManager reference via event
                Dim lEventArgs As New ProjectManagerRequestEventArgs()
                RaiseEvent ProjectManagerRequested(Me, lEventArgs)
                
                If lEventArgs.HasProjectManager Then
                    ' Delegate to ProjectManager for centralized parsing
                    Console.WriteLine($"SourceFileInfo.LoadAndParse: Delegating to ProjectManager for {FileName}")
                    Return lEventArgs.ProjectManager.ParseFile(Me)
                Else
                    Console.WriteLine($"SourceFileInfo.LoadAndParse: No ProjectManager available for {FileName}")
                    Console.WriteLine($"  Parsing requires ProjectManager for proper context")
                    Return False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.LoadAndParse error: {ex.Message}")
                Return False
            End Try
        End Function
        
       
        ''' <summary>
        ''' Get line text
        ''' </summary>
        Public Function GetLineText(vLineIndex As Integer) As String
            Try
                If vLineIndex >= 0 AndAlso vLineIndex < TextLines.Count Then
                    Return TextLines(vLineIndex)
                End If
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.GetLineText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Get node at position
        ''' </summary>
        Public Function GetNodeAtPosition(vLine As Integer, vColumn As Integer) As SyntaxNode
            Try
                If SyntaxTree Is Nothing Then Return Nothing
                Return FindNodeAtPosition(SyntaxTree, vLine, vColumn)
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.GetNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function FindNodeAtPosition(vNode As SyntaxNode, vLine As Integer, vColumn As Integer) As SyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing
                
                ' Check if position is within this node
                If vLine >= vNode.StartLine AndAlso vLine <= vNode.EndLine Then
                    ' Check children for more specific match
                    If vNode.Children IsNot Nothing Then
                        for each lChild in vNode.Children
                            Dim lResult As SyntaxNode = FindNodeAtPosition(lChild, vLine, vColumn)
                            If lResult IsNot Nothing Then
                                Return lResult
                            End If
                        Next
                    End If
                    
                    ' No child contains the position, return this node
                    Return vNode
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        
        ''' <summary>
        ''' Merge this file's syntax tree into the project tree
        ''' </summary>
        Public Sub MergeIntoProjectTree(vRootNamespace As SyntaxNode)
            Try
                If IsDemoMode Then Exit Sub
                If SyntaxTree Is Nothing OrElse vRootNamespace Is Nothing Then
                    Console.WriteLine($"Cannot merge {FileName}: no syntax tree or root namespace")
                    Return
                End If
                
                ' If the file has top-level nodes, merge them
                for each lNode in SyntaxTree.Children
                    If lNode.NodeType = CodeNodeType.eNamespace AndAlso 
                       lNode.Name = vRootNamespace.Name AndAlso 
                       lNode.IsImplicit Then
                        ' This is the implicit root namespace - merge its children directly
                        for each lChild in lNode.Children
                            MergeNodeIntoProject(lChild, vRootNamespace)
                        Next
                    Else
                        ' This is a regular node - merge it
                        MergeNodeIntoProject(lNode, vRootNamespace)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"MergeIntoProjectTree error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get nodes at a specific line
        ''' </summary>
        Public Function GetNodesAtLine(vLine As Integer) As List(Of SyntaxNode)
            Dim lNodes As New List(Of SyntaxNode)()
            
            Try
                If SyntaxTree IsNot Nothing Then
                    CollectNodesAtLine(SyntaxTree, vLine, lNodes)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"GetNodesAtLine error: {ex.Message}")
            End Try
            
            Return lNodes
        End Function
        
        ''' <summary>
        ''' Set the project root namespace for parsing
        ''' </summary>
        Public Sub SetProjectRootNamespace(vRootNamespace As String)
            If IsDemoMode Then Exit Sub
            If Not String.IsNullOrEmpty(vRootNamespace) Then
                ProjectRootNamespace = vRootNamespace
            End If
        End Sub
        
        ' Helper method to merge a node into the project tree
        Private Sub MergeNodeIntoProject(vNode As SyntaxNode, vParentNode As SyntaxNode)
            Try
                If IsDemoMode Then Exit Sub
                If vNode Is Nothing OrElse vParentNode Is Nothing Then Return
                
                ' Check if a similar node already exists
                Dim lExistingNode As SyntaxNode = Nothing
                for each lChild in vParentNode.Children
                    If lChild.Name = vNode.Name AndAlso lChild.NodeType = vNode.NodeType Then
                        lExistingNode = lChild
                        Exit for
                    End If
                Next
                
                If lExistingNode IsNot Nothing Then
                    ' Merge children into existing node
                    for each lChild in vNode.Children
                        MergeNodeIntoProject(lChild, lExistingNode)
                    Next
                Else
                    ' Add the node
                    vParentNode.AddChild(vNode)
                    
                    ' Set file path attribute
                    If vNode.Attributes Is Nothing Then
                        vNode.Attributes = New Dictionary(Of String, String)()
                    End If
                    vNode.Attributes("FilePath") = FilePath
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNodeIntoProject error: {ex.Message}")
            End Try
        End Sub
        
        ' Helper method to collect nodes at a line
        Private Sub CollectNodesAtLine(vNode As SyntaxNode, vLine As Integer, vNodes As List(Of SyntaxNode))
            Try
                If vNode Is Nothing Then Return
                
                ' Check if this node is at the line
                If vNode.StartLine <= vLine AndAlso vNode.EndLine >= vLine Then
                    vNodes.Add(vNode)
                End If
                
                ' Recursively check children
                for each lChild in vNode.Children
                    CollectNodesAtLine(lChild, vLine, vNodes)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CollectNodesAtLine error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Save content to disk
        ''' </summary>
        Public Function SaveContent() As Boolean
            Try
                If IsDemoMode Then Return True
                If String.IsNullOrEmpty(FilePath) Then
                    Console.WriteLine($"Cannot save {FileName}: no file path")
                    Return False
                End If
                
                ' Ensure directory exists
                Dim lDirectory As String = Path.GetDirectoryName(FilePath)
                If Not String.IsNullOrEmpty(lDirectory) AndAlso Not Directory.Exists(lDirectory) Then
                    Directory.CreateDirectory(lDirectory)
                End If
                
                ' Write to file
                File.WriteAllText(FilePath, Content, New System.Text.UTF8Encoding(False))
                
                Console.WriteLine($"Saved {Content.Length} characters to {FileName}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.SaveContent error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Load content from a stream
        ''' </summary>
        Public Sub LoadFromStream(vStream As Stream, vEncoding As Encoding)
            Try
                If IsDemoMode Then Exit Sub
                Using lReader As New StreamReader(vStream, vEncoding)
                    Content = lReader.ReadToEnd()
                    pTextLines = New List(Of String)(Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                    If TextLines.Count = 0 Then
                        TextLines.Add("")
                    End If
                    for I As Integer = 0 To pTextLines.Count - 1
                        UpdateTextLineWithCaseCorrection(I)
                    Next
                    IsLoaded = True
                    NeedsParsing = True
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.LoadFromStream error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Check if the in-memory content differs from disk content
        ''' </summary>
        ''' <returns>True if content differs, False if same or file doesn't exist</returns>
        Public Function HasDiskChanges() As Boolean
            Try
                If IsDemoMode Then 
                    Return False
                End If
                
                If Not File.Exists(FilePath) Then
                    Return False
                End If
                
                Dim lDiskContent As String = File.ReadAllText(FilePath)
                Return lDiskContent <> Content
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.HasDiskChanges error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Debug method to show content loading status
        ''' </summary>
        Public Sub DebugContent()
            Try
'                 Console.WriteLine($"SourceFileInfo Debug: {FileName}")
'                 Console.WriteLine($"  FilePath: {FilePath}")
'                 Console.WriteLine($"  IsLoaded: {IsLoaded}")
'                 Console.WriteLine($"  Content Length: {If(Content IsNot Nothing, Content.Length, 0)}")
'                 Console.WriteLine($"  TextLines Count: {If(pTextLines IsNot Nothing, pTextLines.Count, 0)}")
                
                If pTextLines IsNot Nothing AndAlso pTextLines.Count > 0 Then
                    Console.WriteLine($"  First 5 non-empty lines:")
                    Dim lCount As Integer = 0
                    for each lLine in pTextLines
                        If Not String.IsNullOrWhiteSpace(lLine) AndAlso Not lLine.TrimStart().StartsWith("'") Then
                            'Console.WriteLine($"    Line {pTextLines.IndexOf(lLine)}: {lLine.Substring(0, Math.Min(60, lLine.Length))}")
                            lCount += 1
                            If lCount >= 5 Then Exit For
                        End If
                    Next
                Else
                    Console.WriteLine("  WARNING: No text lines available!")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DebugContent error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Sets the entire text content and updates all related properties
        ''' </summary>
        ''' <param name="vText">The complete text content to set</param>
        ''' <remarks>
        ''' This method updates Content, TextLines, and LineMetadata arrays.
        ''' It marks the file as modified and needing re-parsing.
        ''' </remarks>
        Public Sub SetAllText(vText As String)
            Try
                ' Split into lines
                If String.IsNullOrEmpty(vText) Then
                    pTextLines = New List(Of String) From {""}
                Else
                    ' Handle different line endings
                    vText = vText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
                    pTextLines = New List(Of String)(vText.Split({vbLf}, StringSplitOptions.None))
                End If
                
                ' Ensure we have at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
                ' Reinitialize arrays for new content
                ReDim pLineMetadata(pTextLines.Count - 1)
                
                ' Initialize metadata for each line
                For i As Integer = 0 To pTextLines.Count - 1
                    pLineMetadata(i) = New LineMetadata
                    pLineMetadata(i).IsChanged = True
                    pLineMetadata(i).UpdateHash(pTextLines(i))
                Next
                
                ' Mark as modified and needs parsing
                IsModified = True
                NeedsParsing = True
                
                ' Request async parse for proper syntax highlighting
                RequestAsyncParse()
                
                ' Raise text changed event
                Dim lArgs As New TextLinesChangedEventArgs() With {
                    .ChangeType = TextChangeType.eCompleteReplace,
                    .StartLine = 0,
                    .EndLine = pTextLines.Count - 1,
                    .LinesAffected = pTextLines.Count,
                    .NewLineCount = pTextLines.Count
                }
                RaiseEvent TextLinesChanged(Me, lArgs)
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.SetText error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets the entire text content as a single string
        ''' </summary>
        ''' <returns>The complete text content with proper line endings</returns>
        ''' <remarks>
        ''' Reconstructs the full text from TextLines using Environment.NewLine.
        ''' If no content is loaded, attempts to load from file first.
        ''' </remarks>
        Public Function GetAllText() As String
            Try
                ' If not loaded and not in demo mode, try to load content first
                If Not IsLoaded AndAlso Not IsDemoMode Then
                    If Not LoadContent() Then
                        Console.WriteLine($"GetAllText: Failed To load content for {FileName}")
                        Return ""
                    End If
                End If
                
                ' If we have TextLines, join them with proper line endings
                If TextLines IsNot Nothing AndAlso TextLines.Count > 0 Then
                    ' Use Environment.NewLine for proper platform line endings
                    Dim lResult As String = String.Join(Environment.NewLine, TextLines)
                    
                    ' Update Content property to stay in sync
                    Content = lResult
                    
                    Return lResult
                End If
                
                ' Fall back to Content property if TextLines is empty
                If Not String.IsNullOrEmpty(Content) Then
                    Return Content
                End If
                
                ' Return empty string if no content available
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.GetAllText error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Updates a line of text and marks it as changed
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vNewText">The new text for the line</param>
        ''' <remarks>
        ''' Updates TextLines and resizes CharacterColors array as needed
        ''' </remarks>
        Public Sub UpdateTextLine(vLineIndex As Integer, vNewText As String)
            Try
                ' Validate index
                If vLineIndex < 0 OrElse vLineIndex >= pTextLines.Count Then
                    Console.WriteLine($"UpdateTextLine: Invalid line index {vLineIndex}")
                    Return
                End If
                
                ' Store old line length for comparison
                Dim lOldLength As Integer = If(pTextLines(vLineIndex)?.Length, 0)
                
                ' Update the text
                pTextLines(vLineIndex) = If(vNewText, "")
                UpdateTextLineWithCaseCorrection(vLineIndex)
                Dim lNewLength As Integer = pTextLines(vLineIndex).Length
                
                ' Update LineMetadata for this line
                If pLineMetadata IsNot Nothing AndAlso vLineIndex < pLineMetadata.Length Then
                    pLineMetadata(vLineIndex).MarkChanged()
                    pLineMetadata(vLineIndex).UpdateHash(pTextLines(vLineIndex))
                    pLineMetadata(vLineIndex).ParseState = LineParseState.eUnparsed
                End If
                
                ' Mark file as modified
                IsModified = True
                NeedsParsing = True
                
                ' Update modified timestamp
                LastModified = DateTime.Now
                
                ' Raise event
                Dim lArgs As New TextLinesChangedEventArgs() With {
                    .ChangeType = TextChangeType.eLineModified,
                    .StartLine = vLineIndex,
                    .EndLine = vLineIndex,
                    .LinesAffected = 1,
                    .NewLineCount = pTextLines.Count
                }
                RaiseEvent TextLinesChanged(Me, lArgs)
                
                ' Request async re-parse for this line
                RequestAsyncParse()
                
            Catch ex As Exception
                Console.WriteLine($"UpdateTextLine error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' Removes a line at the specified index
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based index of the line to remove</param>
        Public Sub RemoveLine(vLineIndex As Integer)
            Try
                ' Validate index
                If TextLines Is Nothing OrElse vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then
                    Return
                End If
                
                ' Remove the line
                TextLines.RemoveAt(vLineIndex)
                
                ' Ensure at least one line remains
                If TextLines.Count = 0 Then
                    TextLines.Add("")
                End If
                
                ' Update LineMetadata array
                If LineMetadata IsNot Nothing AndAlso vLineIndex < LineMetadata.Length Then
                    ' Shift metadata up
                    For i As Integer = vLineIndex To LineMetadata.Length - 2
                        LineMetadata(i) = LineMetadata(i + 1)
                    Next
                    
                    ' Resize array
                    ReDim Preserve pLineMetadata(TextLines.Count - 1)
                End If
                
                ' Mark as modified
                IsModified = True
                NeedsParsing = True
                
                ' Update Content
                Content = String.Join(Environment.NewLine, TextLines)
                
                ' Update modified timestamp
                LastModified = DateTime.Now
                
            Catch ex As Exception
                Console.WriteLine($"RemoveLine error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Inserts text at the specified position
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vColumn">Zero-based column index</param>
        ''' <param name="vText">Text to insert</param>
        Public Sub InsertText(vLineIndex As Integer, vColumn As Integer, vText As String)
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then Return
                If String.IsNullOrEmpty(vText) Then Return
                
                ' Handle multi-line insertions
                If vText.Contains(Environment.NewLine) OrElse vText.Contains(vbLf) Then
                    ' Complex multi-line insertion
                    InsertMultiLineText(vLineIndex, vColumn, vText)
                Else
                    ' Simple single-line insertion
                    InsertTextInLine(vLineIndex, vColumn, vText)
                End If
                
                ' Mark as modified
                IsModified = True
                NeedsParsing = True
                
                ' Request async parse for proper highlighting
                RequestAsyncParse()
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.InsertText error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Inserts text within a line at the specified column
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vColumn">Column position where to insert</param>
        ''' <param name="vText">Text to insert</param>
        Public Sub InsertTextInLine(vLineIndex As Integer, vColumn As Integer, vText As String)
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then
                    Console.WriteLine($"InsertTextInLine: Invalid line index {vLineIndex}")
                    Return
                End If
                
                ' Get the line
                Dim lLine As String = TextLines(vLineIndex)
                
                ' Validate and adjust column
                vColumn = Math.Max(0, Math.Min(vColumn, lLine.Length))
                
                ' Insert the text
                Dim lNewLine As String
                If vColumn = 0 Then
                    lNewLine = vText & lLine
                ElseIf vColumn >= lLine.Length Then
                    lNewLine = lLine & vText
                Else
                    lNewLine = lLine.Substring(0, vColumn) & vText & lLine.Substring(vColumn)
                End If
                
                ' Update the line
                TextLines(vLineIndex) = lNewLine
                
                ' Update LineMetadata
                If LineMetadata IsNot Nothing AndAlso vLineIndex < LineMetadata.Length Then
                    LineMetadata(vLineIndex).MarkChanged()
                    LineMetadata(vLineIndex).UpdateHash(lNewLine)
                End If
                
'                 ' Update CharacterColors if needed
'                 If CharacterColors IsNot Nothing AndAlso vLineIndex < CharacterColors.Length Then
'                     Dim lOldLength As Integer = lLine.Length
'                     Dim lNewLength As Integer = lNewLine.Length
'                     
'                     If lNewLength <> lOldLength Then
'                         ' Resize character colors array for this line
'                         Dim lOldColors() As CharacterColorInfo = CharacterColors(vLineIndex)
'                         ReDim CharacterColors(vLineIndex)(Math.Max(0, lNewLength - 1))
'                         
'                         ' Copy colors before insertion point
'                         For i As Integer = 0 To Math.Min(vColumn - 1, lOldLength - 1)
'                             If i < lOldColors.Length AndAlso lOldColors(i) IsNot Nothing Then
'                                 CharacterColors(vLineIndex)(i) = lOldColors(i)
'                             Else
'                                 CharacterColors(vLineIndex)(i) = New CharacterColorInfo()
'                             End If
'                         Next
'                         
'                         ' Initialize colors for inserted text
'                         For i As Integer = vColumn To vColumn + vText.Length - 1
'                             CharacterColors(vLineIndex)(i) = New CharacterColorInfo()
'                         Next
'                         
'                         ' Copy colors after insertion point
'                         For i As Integer = vColumn To lOldLength - 1
'                             Dim lNewIndex As Integer = i + vText.Length
'                             If lNewIndex < lNewLength AndAlso i < lOldColors.Length AndAlso lOldColors(i) IsNot Nothing Then
'                                 CharacterColors(vLineIndex)(lNewIndex) = lOldColors(i)
'                             ElseIf lNewIndex < lNewLength Then
'                                 CharacterColors(vLineIndex)(lNewIndex) = New CharacterColorInfo()
'                             End If
'                         Next
'                     End If
'                 End If
                
                ' Mark as modified
                IsModified = True
                NeedsParsing = True
                
                ' Raise event
                Dim lArgs As New TextLinesChangedEventArgs() With {
                    .ChangeType = TextChangeType.eLineModified,
                    .StartLine = vLineIndex,
                    .EndLine = vLineIndex,
                    .LinesAffected = 1,
                    .NewLineCount = TextLines.Count
                }
                RaiseEvent TextLinesChanged(Me, lArgs)
                
            Catch ex As Exception
                Console.WriteLine($"InsertTextInLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Inserts multi-line text at the specified position with proper syntax coloring
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index where to start insertion</param>
        ''' <param name="vColumn">Column position in the line where to insert</param>
        ''' <param name="vText">Multi-line text to insert</param>
        Public Sub InsertMultiLineText(vLineIndex As Integer, vColumn As Integer, vText As String)
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then
                    Console.WriteLine($"InsertMultiLineText: Invalid line index {vLineIndex}")
                    Return
                End If
                
                If String.IsNullOrEmpty(vText) Then Return
                
                ' Split the text into lines
                Dim lNewLines() As String = vText.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                
                If lNewLines.Length = 0 Then Return
                
                ' Get the current line
                Dim lCurrentLine As String = TextLines(vLineIndex)
                vColumn = Math.Max(0, Math.Min(vColumn, lCurrentLine.Length))
                
                ' Split current line at insertion point
                Dim lBeforeInsert As String = If(vColumn > 0, lCurrentLine.Substring(0, vColumn), "")
                Dim lAfterInsert As String = If(vColumn < lCurrentLine.Length, lCurrentLine.Substring(vColumn), "")
                
                ' Store the starting line for coloring
                Dim lStartLine As Integer = vLineIndex
                Dim lEndLine As Integer
                
                If lNewLines.Length = 1 Then
                    ' Single line - just insert in current line
                    InsertTextInLine(vLineIndex, vColumn, lNewLines(0))
                    lEndLine = vLineIndex
                Else
                    ' Multi-line insertion
                    ' First: Update current line with first part + first new line
                    TextLines(vLineIndex) = lBeforeInsert & lNewLines(0)
                    
                    ' Update metadata for modified line
                    If LineMetadata IsNot Nothing AndAlso vLineIndex < LineMetadata.Length Then
                        LineMetadata(vLineIndex).MarkChanged()
                        LineMetadata(vLineIndex).UpdateHash(TextLines(vLineIndex))
                    End If
                    
                    ' Insert middle lines
                    Dim lInsertPosition As Integer = vLineIndex + 1
                    For i As Integer = 1 To lNewLines.Length - 2
                        InsertLine(lInsertPosition, lNewLines(i))
                        lInsertPosition += 1
                    Next
                    
                    ' Insert last line combined with remainder of original line
                    InsertLine(lInsertPosition, lNewLines(lNewLines.Length - 1) & lAfterInsert)
                    
                    lEndLine = vLineIndex + lNewLines.Length - 1
                    
                    ' Mark as modified
                    IsModified = True
                    NeedsParsing = True
'                     
'                     ' CRITICAL FIX: Ensure CharacterColors arrays are properly sized for all inserted lines
'                     ' This prevents display issues while waiting for async parse
'                     If pCharacterColors IsNot Nothing Then
'                         ' Ensure array is properly sized
'                         If pCharacterColors.Length < TextLines.Count Then
'                             ReDim Preserve pCharacterColors(TextLines.Count - 1)
'                         End If
'                         
'                         ' Initialize character colors for all affected lines with default color
'                         Dim lDefaultColor As String = GetDefaultForegroundColor()
'                         For lineIdx As Integer = lStartLine To Math.Min(lEndLine, TextLines.Count - 1)
'                             Dim lLineText As String = TextLines(lineIdx)
'                             Dim lLineLength As Integer = lLineText.Length
'                             
'                             If lLineLength > 0 Then
'                                 ' Ensure this line has a properly sized color array
'                                 If pCharacterColors(lineIdx) Is Nothing OrElse pCharacterColors(lineIdx).Length <> lLineLength Then
'                                     ReDim pCharacterColors(lineIdx)(lLineLength - 1)
'                                 End If
'                                 
'                                 ' Set all characters to default color initially
'                                 For charIdx As Integer = 0 To lLineLength - 1
'                                     pCharacterColors(lineIdx)(charIdx) = New CharacterColorInfo(lDefaultColor)
'                                 Next
'                             Else
'                                 pCharacterColors(lineIdx) = New CharacterColorInfo() {}
'                             End If
'                         Next
'                         
'                         Console.WriteLine($"InsertMultiLineText: Initialized colors for lines {lStartLine} To {lEndLine}")
'                     End If
                    
                    ' Raise event for multiple lines changed
                    Dim lArgs As New TextLinesChangedEventArgs() With {
                        .ChangeType = TextChangeType.eMultipleLines,
                        .StartLine = vLineIndex,
                        .EndLine = lEndLine,
                        .LinesAffected = lNewLines.Length,
                        .NewLineCount = TextLines.Count
                    }
                    RaiseEvent TextLinesChanged(Me, lArgs)
                End If
                
                ' CRITICAL: Request immediate async parse for proper syntax coloring
                ' This is especially important for large paste operations
                RequestAsyncParse()
                Console.WriteLine($"InsertMultiLineText: Requested async parse for {lNewLines.Length} New lines")
                
            Catch ex As Exception
                Console.WriteLine($"InsertMultiLineText error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets the text content (alias for SetAllText for compatibility)
        ''' </summary>
        ''' <param name="vText">The complete text to set</param>
        Public Sub SetText(vText As String)
            SetAllText(vText)
        End Sub
        
        ''' <summary>
        ''' Deletes text within a line
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vStartColumn">Start column (inclusive)</param>
        ''' <param name="vEndColumn">End column (exclusive)</param>
        Public Sub DeleteTextInLine(vLineIndex As Integer, vStartColumn As Integer, vEndColumn As Integer)
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then
                    Console.WriteLine($"DeleteTextInLine: Invalid line index {vLineIndex}")
                    Return
                End If
                
                ' Get the line
                Dim lLine As String = TextLines(vLineIndex)
                
                ' Validate and adjust columns
                vStartColumn = Math.Max(0, Math.Min(vStartColumn, lLine.Length))
                vEndColumn = Math.Max(vStartColumn, Math.Min(vEndColumn, lLine.Length))
                
                ' Delete the text
                Dim lNewLine As String
                If vStartColumn = 0 AndAlso vEndColumn >= lLine.Length Then
                    lNewLine = ""
                ElseIf vStartColumn = 0 Then
                    lNewLine = lLine.Substring(vEndColumn)
                ElseIf vEndColumn >= lLine.Length Then
                    lNewLine = lLine.Substring(0, vStartColumn)
                Else
                    lNewLine = lLine.Substring(0, vStartColumn) & lLine.Substring(vEndColumn)
                End If
                
                ' Update the line
                TextLines(vLineIndex) = lNewLine
                
                ' Update LineMetadata
                If LineMetadata IsNot Nothing AndAlso vLineIndex < LineMetadata.Length Then
                    LineMetadata(vLineIndex).MarkChanged()
                    LineMetadata(vLineIndex).UpdateHash(lNewLine)
                End If
                
'                 ' Update CharacterColors if needed
'                 If CharacterColors IsNot Nothing AndAlso vLineIndex < CharacterColors.Length Then
'                     Dim lNewLength As Integer = lNewLine.Length
'                     
'                     If lNewLength > 0 Then
'                         Dim lOldColors() As CharacterColorInfo = CharacterColors(vLineIndex)
'                         ReDim CharacterColors(vLineIndex)(lNewLength - 1)
'                         
'                         ' Copy colors before deletion point
'                         For i As Integer = 0 To Math.Min(vStartColumn - 1, lNewLength - 1)
'                             If i < lOldColors.Length AndAlso lOldColors(i) IsNot Nothing Then
'                                 CharacterColors(vLineIndex)(i) = lOldColors(i)
'                             Else
'                                 CharacterColors(vLineIndex)(i) = New CharacterColorInfo()
'                             End If
'                         Next
'                         
'                         ' Copy colors after deletion point
'                         Dim lDeletedCount As Integer = vEndColumn - vStartColumn
'                         For i As Integer = vStartColumn To lNewLength - 1
'                             Dim lOldIndex As Integer = i + lDeletedCount
'                             If lOldIndex < lOldColors.Length AndAlso lOldColors(lOldIndex) IsNot Nothing Then
'                                 CharacterColors(vLineIndex)(i) = lOldColors(lOldIndex)
'                             Else
'                                 CharacterColors(vLineIndex)(i) = New CharacterColorInfo()
'                             End If
'                         Next
'                     Else
'                         CharacterColors(vLineIndex) = New CharacterColorInfo() {}
'                     End If
'                 End If
                
                ' Mark as modified
                IsModified = True
                NeedsParsing = True
                
                ' Raise event
                Dim lArgs As New TextLinesChangedEventArgs() With {
                    .ChangeType = TextChangeType.eLineModified,
                    .StartLine = vLineIndex,
                    .EndLine = vLineIndex,
                    .LinesAffected = 1,
                    .NewLineCount = TextLines.Count
                }
                RaiseEvent TextLinesChanged(Me, lArgs)
                
            Catch ex As Exception
                Console.WriteLine($"DeleteTextInLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Replaces text in a line
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vStartColumn">Start column (inclusive)</param>
        ''' <param name="vEndColumn">End column (exclusive)</param>
        ''' <param name="vNewText">Text to insert in place of deleted text</param>
        Public Sub ReplaceTextInLine(vLineIndex As Integer, vStartColumn As Integer, vEndColumn As Integer, vNewText As String)
            Try
                ' Delete the old text
                DeleteTextInLine(vLineIndex, vStartColumn, vEndColumn)
                
                ' Insert the new text
                InsertTextInLine(vLineIndex, vStartColumn, If(vNewText, ""))
                
            Catch ex As Exception
                Console.WriteLine($"ReplaceTextInLine error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Ensures all internal data structures are properly initialized and sized
        ''' </summary>
        ''' <remarks>
        ''' Call this after loading content to ensure LineMetadata and CharacterColors are ready
        ''' </remarks>
        Public Sub EnsureDataStructures()
            Try
                ' Ensure TextLines exists
                If pTextLines Is Nothing Then
                    pTextLines = New List(Of String)()
                End If
                
                ' Ensure at least one line
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
                Dim lLineCount As Integer = pTextLines.Count
                
                ' Ensure LineMetadata is properly sized
                EnsureLineMetadata()
                
                Console.WriteLine($"EnsureDataStructures: {FileName} has {lLineCount} lines, metadata and colors initialized")
                
            Catch ex As Exception
                Console.WriteLine($"EnsureDataStructures error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Gets the LineMetadata for a specific line
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <returns>LineMetadata for the line, or a new instance if not available</returns>
        ''' <remarks>
        ''' Ensures both LineMetadata and CharacterTokens arrays are properly sized
        ''' if they need to be extended to accommodate the requested index
        ''' </remarks>
        Public Function GetLineMetadata(vLineIndex As Integer) As LineMetadata
            Try
                ' CRITICAL FIX: Check for Nothing BEFORE accessing Length
                If pLineMetadata Is Nothing OrElse pLineMetadata.Length = 0 Then 
                    EnsureLineMetadata()
                End If
                
                ' Validate index
                If vLineIndex < 0 OrElse pLineMetadata Is Nothing Then
                    Console.WriteLine($"GetLineMetadata: Invalid index {vLineIndex} Or uninitialized array")
                    Return New LineMetadata()
                End If
                
                ' Check array bounds
                If vLineIndex >= pLineMetadata.Length Then
                    Console.WriteLine($"GetLineMetadata: Index {vLineIndex} out Of bounds (array length={pLineMetadata.Length})")
                    
                    ' Resize both arrays to accommodate
                    ReDim Preserve pLineMetadata(vLineIndex)
                    ReDim Preserve pCharacterTokens(vLineIndex)
                    
                    For i As Integer = 0 To vLineIndex
                        If pLineMetadata(i) Is Nothing Then
                            pLineMetadata(i) = New LineMetadata()
                        End If
                        
                        ' Initialize CharacterTokens for this line if needed
                        If i < TextLines.Count Then
                            Dim lLineLength As Integer = TextLines(i).Length
                            If pCharacterTokens(i) Is Nothing OrElse pCharacterTokens(i).Length <> lLineLength Then
                                If lLineLength > 0 Then
                                    ReDim pCharacterTokens(i)(lLineLength - 1)
                                    Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                                    For j As Integer = 0 To lLineLength - 1
                                        pCharacterTokens(i)(j) = lDefaultToken
                                    Next
                                Else
                                    pCharacterTokens(i) = New Byte() {}
                                End If
                            End If
                        End If
                    Next
                End If
                
                ' Ensure the specific element is not Nothing
                If pLineMetadata(vLineIndex) Is Nothing Then
                    pLineMetadata(vLineIndex) = New LineMetadata()
                    ' Update hash if we have the text
                    If TextLines IsNot Nothing AndAlso vLineIndex < TextLines.Count Then
                        pLineMetadata(vLineIndex).UpdateHash(TextLines(vLineIndex))
                    End If
                End If
                
                Return pLineMetadata(vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"GetLineMetadata error: {ex.Message}")
                Return New LineMetadata()
            End Try
        End Function
        
        ''' <summary>
        ''' Reloads the file content from disk, discarding any in-memory changes
        ''' </summary>
        ''' <returns>True if successfully reloaded, False if file doesn't exist or error occurred</returns>
        ''' <remarks>
        ''' This method is used when the user closes an editor without saving changes.
        ''' It reverts the SourceFileInfo to the last saved version on disk.
        ''' Properly resets both LineMetadata and CharacterTokens arrays.
        ''' </remarks>
        Public Function ReloadFile() As Boolean
            Try
                ' Check if file exists
                If Not File.Exists(FilePath) Then
                    Console.WriteLine($"ReloadFile: File not found - {FilePath}")
                    Return False
                End If
                
                ' Store loaded state before reset
                Dim lWasLoaded As Boolean = IsLoaded
                
                ' Read file content with encoding detection
                Dim lBytes As Byte() = File.ReadAllBytes(FilePath)
                Dim lEncoding As Encoding = DetectEncoding(lBytes)
                pFileEncoding = lEncoding
                
                ' Convert to string using detected encoding
                Content = lEncoding.GetString(lBytes)
                
                ' Split into lines
                Dim lLines As String() = Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                TextLines = New List(Of String)(lLines)
                
                ' Ensure at least one line exists
                If TextLines.Count = 0 Then
                    TextLines.Add("")
                End If
                
                ' Reset metadata arrays to match new line count
                Dim lLineCount As Integer = TextLines.Count
                
                ' Reset LineMetadata array
                If pLineMetadata IsNot Nothing OrElse lWasLoaded Then
                    ReDim pLineMetadata(Math.Max(0, lLineCount - 1))
                    For i As Integer = 0 To pLineMetadata.Length - 1
                        pLineMetadata(i) = New LineMetadata()
                        ' Initialize hash for each line
                        pLineMetadata(i).UpdateHash(TextLines(i))
                    Next
                End If
                
                ' SYNC: Reset CharacterTokens array
                If pCharacterTokens IsNot Nothing OrElse lWasLoaded Then
                    ReDim pCharacterTokens(Math.Max(0, lLineCount - 1))
                    For i As Integer = 0 To lLineCount - 1
                        Dim lLineLength As Integer = TextLines(i).Length
                        If lLineLength > 0 Then
                            ReDim pCharacterTokens(i)(lLineLength - 1)
                            Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                            For j As Integer = 0 To lLineLength - 1
                                pCharacterTokens(i)(j) = lDefaultToken
                            Next
                        Else
                            pCharacterTokens(i) = New Byte() {}
                        End If
                    Next
                End If
                
                ' Reset file state flags
                IsLoaded = True
                IsModified = False  ' File is now clean (matches disk)
                NeedsParsing = True  ' Content changed, needs re-parsing
                IsParsed = False
                
                ' Clear any existing parse results
                SyntaxTree = Nothing
                ParseResult = Nothing
                ParseErrors?.Clear()
                
                ' Update timestamps
                LastModified = DateTime.Now
                LastParsed = DateTime.MinValue  ' Reset to indicate not parsed
                
                ' Get file info for more accurate timestamp
                Try
                    Dim lFileInfo As New FileInfo(FilePath)
                    LastModified = lFileInfo.LastWriteTime
                Catch
                    ' Keep the DateTime.Now if we can't get file info
                End Try
                
                Console.WriteLine($"ReloadFile: Successfully reloaded {FileName}")
                Console.WriteLine($"  Content length: {Content.Length} characters")
                Console.WriteLine($"  Line count: {TextLines.Count}")
                Console.WriteLine($"  IsModified: {IsModified}")
                
                ' Raise content changed event to notify listeners
                RaiseEvent ContentChanged(Me, EventArgs.Empty)
                
                ' CRITICAL FIX: Request async parsing to restore syntax highlighting
                RequestAsyncParse()
                Console.WriteLine($"ReloadFile: Requested async parse for {FileName}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ReloadFile error: {ex.Message}")
                Console.WriteLine($"  Stack trace: {ex.StackTrace}")
                
                ' Try to leave object in a consistent state on error
                Try
                    Content = ""
                    TextLines = New List(Of String) From {""}
                    IsLoaded = False
                    IsModified = False
                    IsParsed = False
                    NeedsParsing = False
                Catch
                    ' Ignore errors during cleanup
                End Try
                
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Ensures LineMetadata and CharacterTokens arrays are properly initialized and sized
        ''' </summary>
        ''' <remarks>
        ''' This method ensures both arrays are in sync with the TextLines count.
        ''' It's called whenever the line count changes to maintain consistency.
        ''' </remarks>
        Private Sub EnsureLineMetadata()
            Try
                Dim lLineCount As Integer = TextLines.Count
                
                ' TextLines property ensures at least one line
                If lLineCount = 0 Then 
                    Return
                End If
                
                ' Check if we need to do anything for LineMetadata
                If pLineMetadata IsNot Nothing AndAlso pLineMetadata.Length = lLineCount Then
                    ' LineMetadata is properly sized, just ensure no null entries
                    Dim lHasNulls As Boolean = False
                    For i As Integer = 0 To lLineCount - 1
                        If pLineMetadata(i) Is Nothing Then
                            pLineMetadata(i) = New LineMetadata()
                            pLineMetadata(i).UpdateHash(TextLines(i))
                            lHasNulls = True
                        End If
                    Next
                Else
                    ' Initialize or resize LineMetadata array
                    If pLineMetadata Is Nothing Then
                        ReDim pLineMetadata(lLineCount - 1)
                    Else
                        ReDim Preserve pLineMetadata(lLineCount - 1)
                    End If
                    
                    ' Initialize any null entries
                    For i As Integer = 0 To lLineCount - 1
                        If pLineMetadata(i) Is Nothing Then
                            pLineMetadata(i) = New LineMetadata()
                        End If
                        ' Update hash for the line
                        If i < TextLines.Count Then
                            pLineMetadata(i).UpdateHash(TextLines(i))
                        End If
                    Next
                End If
                
                ' SYNC CharacterTokens array with LineMetadata
                If pCharacterTokens Is Nothing OrElse pCharacterTokens.Length <> lLineCount Then
                    If pCharacterTokens Is Nothing Then
                        ReDim pCharacterTokens(lLineCount - 1)
                    Else
                        ReDim Preserve pCharacterTokens(lLineCount - 1)
                    End If
                End If
                
                ' Ensure each line in CharacterTokens has proper token array
                For i As Integer = 0 To lLineCount - 1
                    Dim lLineLength As Integer = If(TextLines(i)?.Length, 0)
                    
                    If pCharacterTokens(i) Is Nothing OrElse pCharacterTokens(i).Length <> lLineLength Then
                        If lLineLength > 0 Then
                            ReDim pCharacterTokens(i)(lLineLength - 1)
                            ' Initialize with default normal token type
                            Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                            For j As Integer = 0 To lLineLength - 1
                                pCharacterTokens(i)(j) = lDefaultToken
                            Next
                        Else
                            pCharacterTokens(i) = New Byte() {}
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"EnsureLineMetadata error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates the LineMetadata array with new data
        ''' </summary>
        ''' <param name="vNewLineMetadata">The new metadata to copy</param>
        Public Sub UpdateLineMetadata(vNewLineMetadata As LineMetadata())
            If vNewLineMetadata Is Nothing Then
                pLineMetadata = Nothing
                Return
            End If
            
            ' Resize if needed
            If pLineMetadata Is Nothing OrElse pLineMetadata.Length <> vNewLineMetadata.Length Then
                ReDim pLineMetadata(vNewLineMetadata.Length - 1)
            End If
            
            ' Copy the contents
            for i As Integer = 0 To vNewLineMetadata.Length - 1
                pLineMetadata(i) = vNewLineMetadata(i)
            Next
        End Sub


        

        
        ''' <summary>
        ''' Ensures the LineMetadata and CharacterTokens arrays are properly sized
        ''' </summary>
        ''' <param name="vLineCount">The required number of lines</param>
        ''' <remarks>
        ''' Resizes both LineMetadata and CharacterTokens arrays to the specified count
        ''' </remarks>
        Public Sub EnsureLineMetadataSize(vLineCount As Integer)
            ' Resize LineMetadata
            If pLineMetadata Is Nothing OrElse pLineMetadata.Length <> vLineCount Then
                ReDim Preserve pLineMetadata(vLineCount - 1)
                
                ' Initialize any new entries
                For i As Integer = 0 To pLineMetadata.Length - 1
                    If pLineMetadata(i) Is Nothing Then
                        pLineMetadata(i) = New LineMetadata()
                    End If
                Next
            End If
            
            ' SYNC: Resize CharacterTokens
            If pCharacterTokens Is Nothing OrElse pCharacterTokens.Length <> vLineCount Then
                ReDim Preserve pCharacterTokens(vLineCount - 1)
            End If
            
            ' Ensure each CharacterTokens line is properly sized
            For i As Integer = 0 To vLineCount - 1
                If i < TextLines.Count Then
                    Dim lLineLength As Integer = TextLines(i).Length
                    If pCharacterTokens(i) Is Nothing OrElse pCharacterTokens(i).Length <> lLineLength Then
                        If lLineLength > 0 Then
                            ReDim pCharacterTokens(i)(lLineLength - 1)
                            Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                            For j As Integer = 0 To lLineLength - 1
                                pCharacterTokens(i)(j) = lDefaultToken
                            Next
                        Else
                            pCharacterTokens(i) = New Byte() {}
                        End If
                    End If
                End If
            Next
        End Sub

        ''' <summary>
        ''' Initializes LineMetadata and CharacterTokens arrays with default values
        ''' </summary>
        ''' <remarks>
        ''' Called after loading content to prepare data structures for rendering.
        ''' The arrays are populated with defaults so the editor can display immediately
        ''' while parsing happens asynchronously in the background.
        ''' </remarks>
        Private Sub InitializeDataArrays()
            Try
                Dim lLineCount As Integer = If(pTextLines?.Count, 0)
                
                ' Ensure we have at least one line
                If lLineCount = 0 Then
                    If pTextLines Is Nothing Then pTextLines = New List(Of String)()
                    pTextLines.Add("")
                    lLineCount = 1
                End If
                
                ' Initialize LineMetadata array
                If pLineMetadata Is Nothing OrElse pLineMetadata.Length <> lLineCount Then
                    ReDim pLineMetadata(lLineCount - 1)
                End If
                
                For i As Integer = 0 To lLineCount - 1
                    If pLineMetadata(i) Is Nothing Then
                        pLineMetadata(i) = New LineMetadata()
                    End If
                    ' Initialize hash
                    If i < pTextLines.Count Then
                        pLineMetadata(i).UpdateHash(pTextLines(i))
                    End If
                Next
                
                ' Initialize CharacterTokens array
                If pCharacterTokens Is Nothing OrElse pCharacterTokens.Length <> lLineCount Then
                    ReDim pCharacterTokens(lLineCount - 1)
                End If
                
                ' Initialize token arrays for each line
                For i As Integer = 0 To lLineCount - 1
                    Dim lLineLength As Integer = If(pTextLines(i)?.Length, 0)
                    
                    If lLineLength > 0 Then
                        ReDim pCharacterTokens(i)(lLineLength - 1)
                        Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                        For j As Integer = 0 To lLineLength - 1
                            pCharacterTokens(i)(j) = lDefaultToken
                        Next
                    Else
                        pCharacterTokens(i) = New Byte() {}
                    End If
                Next
                
                Console.WriteLine($"InitializeDataArrays: Initialized {lLineCount} lines with metadata and tokens")
                
            Catch ex As Exception
                Console.WriteLine($"InitializeDataArrays error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                
                ' Ensure minimal state
                If pTextLines Is Nothing Then pTextLines = New List(Of String)({"" })
                If pLineMetadata Is Nothing Then
                    ReDim pLineMetadata(0)
                    pLineMetadata(0) = New LineMetadata()
                End If
                If pCharacterTokens Is Nothing Then
                    ReDim pCharacterTokens(0)
                    pCharacterTokens(0) = New Byte() {}
                End If
            End Try
        End Sub


        
        ''' <summary>
        ''' Requests async parsing from ProjectManager
        ''' </summary>
        ''' <remarks>
        ''' This triggers async parsing and color application through ProjectManager
        ''' </remarks>
        Friend Sub RequestAsyncParse()
            Try
                ' Only request if we need parsing
                If Not NeedsParsing Then Return
                
                If pProjectManager Is Nothing Then
                    ' Try to get from ProjectManager's ThemeManager
                    Dim lArgs As New ProjectManagerRequestEventArgs()
                    RaiseEvent ProjectManagerRequested(Me, lArgs)
                End If
                
                If Not pProjectManager Is Nothing Then
                    ' Request async parse through ProjectManager
                    'Console.WriteLine($"SourceFileInfo.RequestAsyncParse: Requesting parse for {FileName}")
                    
                    ' The ProjectManager will handle this asynchronously
                    ' and update our arrays when complete
                    
                    ' FIXED: Use discard to indicate fire-and-forget is intentional
                    ' This suppresses the BC42358 warning while maintaining the same behavior
                    Dim lParseTask As Task = pProjectManager.ParseFileAsync(Me)
                Else
                    Console.WriteLine($"SourceFileInfo.RequestAsyncParse: No ProjectManager available")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RequestAsyncParse error: {ex.Message}")
            End Try
        End Sub      
  

        
        ''' <summary>
        ''' Notifies listeners that rendering data has changed
        ''' </summary>
        ''' <param name="vStartLine">First line that changed</param>
        ''' <param name="vEndLine">Last line that changed</param>
        ''' <remarks>
        ''' Called after parsing updates to trigger editor redraw
        ''' </remarks>
        Public Sub NotifyRenderingChanged(vStartLine As Integer, vEndLine As Integer)
            Try
                ' Notify through content changed event for now
                ' TODO: Add specific RenderingChanged event if needed
                RaiseEvent ContentChanged(Me, EventArgs.Empty)
                
                'Console.WriteLine($"NotifyRenderingChanged: Lines {vStartLine} To {vEndLine}")
                
            Catch ex As Exception
                Console.WriteLine($"NotifyRenderingChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates the LineMetadata from a parse result
        ''' </summary>
        ''' <param name="vParseResult">The parse result containing new metadata</param>
        ''' <remarks>
        ''' Called by ProjectManager after parsing to update the metadata.
        ''' Also ensures CharacterTokens array stays in sync.
        ''' </remarks>
        Public Sub UpdateLineMetadata(vParseResult As ParseResult)
            Try
                If vParseResult Is Nothing OrElse vParseResult.LineMetadata Is Nothing Then
                    Return
                End If
                
                Dim lNewCount As Integer = vParseResult.LineMetadata.Length
                Dim lCurrentCount As Integer = If(pLineMetadata?.Length, 0)
                
                ' Resize LineMetadata if needed
                If lCurrentCount <> lNewCount Then
                    ReDim pLineMetadata(Math.Max(0, lNewCount - 1))
                End If
                
                ' Copy the metadata
                For i As Integer = 0 To lNewCount - 1
                    pLineMetadata(i) = vParseResult.LineMetadata(i)
                Next
                
                ' SYNC: Ensure CharacterTokens array matches
                If pCharacterTokens Is Nothing OrElse pCharacterTokens.Length <> lNewCount Then
                    ReDim Preserve pCharacterTokens(Math.Max(0, lNewCount - 1))
                End If
                
                ' Update CharacterTokens for each line that has syntax tokens
                For i As Integer = 0 To lNewCount - 1
                    If pLineMetadata(i)?.SyntaxTokens IsNot Nothing AndAlso pLineMetadata(i).SyntaxTokens.Count > 0 Then
                        ' Update tokens for this line
                        UpdateCharacterTokens(i, pLineMetadata(i).SyntaxTokens)
                    Else
                        ' Apply default tokens
                        ApplyDefaultTokens(i)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineMetadata error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Marks a line as changed, requiring re-parsing
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        Private Sub MarkLineChanged(vLineIndex As Integer)
            Try
                If LineMetadata IsNot Nothing AndAlso vLineIndex < LineMetadata.Length Then
                    ' Use the safe GetLineMetadata method
                    Dim lMetadata As LineMetadata = GetLineMetadata(vLineIndex)
                    lMetadata.MarkChanged()
                    
                    ' Notify that content has changed
                    IsModified = True
                    NeedsParsing = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MarkLineChanged error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates a line with automatic case correction for keywords and identifiers
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vNewText">The new text for the line</param>
        ''' <remarks>
        ''' Automatically corrects keyword and identifier casing as part of text storage.
        ''' This is a language feature, not a parsing concern.
        ''' </remarks>
        Public Sub UpdateTextLineWithCaseCorrection(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= TextLines.Count Then Return
                
                Dim vNewText As String = TextLines(vLineIndex)

                ' Apply case corrections if enabled
                Dim lCorrectedText As String = vNewText
                lCorrectedText = ApplyCaseCorrection(vNewText, vLineIndex)
                
                ' Update the line
                TextLines(vLineIndex) = lCorrectedText
                
                ' Mark as modified and needing parse
                IsModified = True
                NeedsParsing = True
                MarkLineChanged(vLineIndex)
                
                ' Notify listeners
                RaiseEvent ContentChanged(Me, EventArgs.Empty)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateTextLineWithCaseCorrection error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Applies case correction to a line of text
        ''' </summary>
        ''' <param name="vText">The text to correct</param>
        ''' <param name="vLineIndex">Line index for context</param>
        ''' <returns>Text with corrected casing</returns>
        Private Function ApplyCaseCorrection(vText As String, vLineIndex As Integer) As String
            Try
                ' Preserve original indentation
                Dim lIndentLength As Integer = vText.Length - vText.TrimStart().Length
                Dim lIndent As String = vText.Substring(0, lIndentLength)
                Dim lContent As String = vText.Substring(lIndentLength)
                
                ' Don't process empty lines or comments
                If String.IsNullOrWhiteSpace(lContent) OrElse lContent.TrimStart().StartsWith("'") Then
                    Return vText
                End If
                
                ' Simple word-based correction (avoids complex parsing)
                Dim lWords As String() = SplitIntoWords(lContent)
                Dim lResult As New StringBuilder(lIndent)
                Dim lInString As Boolean = False
                Dim lLastPos As Integer = 0
                
                for each lWord in lWords
                    Dim lWordStart As Integer = lContent.IndexOf(lWord, lLastPos)
                    
                    ' Add any text before this word (operators, spaces, etc.)
                    If lWordStart > lLastPos Then
                        Dim lBetween As String = lContent.Substring(lLastPos, lWordStart - lLastPos)
                        lResult.Append(lBetween)
                        
                        ' Check if we entered a string
                        If lBetween.Contains("""") Then
                            lInString = Not lInString
                        End If
                    End If
                    
                    ' Apply correction if not in a string
                    If Not lInString Then
                        lResult.Append(CorrectWordCase(lWord))
                    Else
                        lResult.Append(lWord)
                    End If
                    
                    lLastPos = lWordStart + lWord.Length
                Next
                
                ' Add any remaining text
                If lLastPos < lContent.Length Then
                    lResult.Append(lContent.Substring(lLastPos))
                End If
                
                Return lResult.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"ApplyCaseCorrection error: {ex.Message}")
                Return vText ' Return original on error
            End Try
        End Function
        
        ''' <summary>
        ''' Corrects the case of a single word (keyword or identifier)
        ''' </summary>
        Private Function CorrectWordCase(vWord As String) As String
            Try
                ' Try keyword first
                If pKeywordCaseMap Is Nothing Then
                    InitializeKeywordCaseMap()
                End If
                
                Dim lCorrectCase As String = Nothing
                If pKeywordCaseMap.TryGetValue(vWord.ToLower(), lCorrectCase) Then
                    Return lCorrectCase
                End If
                
                ' Try identifier
                If pIdentifierCaseMap.TryGetValue(vWord.ToLower(), lCorrectCase) Then
                    Return lCorrectCase
                End If
                
                ' No correction needed
                Return vWord
                
            Catch ex As Exception
                Return vWord
            End Try
        End Function
        
        ''' <summary>
        ''' Updates the identifier case map from ProjectManager
        ''' </summary>
        ''' <param name="vIdentifierMap">Map of lowercase to proper case identifiers</param>
        Public Sub UpdateIdentifierCaseMap(vIdentifierMap As Dictionary(Of String, String))
            Try
                pIdentifierCaseMap.Clear()
                
                for each lKvp in vIdentifierMap
                    pIdentifierCaseMap(lKvp.Key.ToLower()) = lKvp.Value
                Next
                
               ' Console.WriteLine($"SourceFileInfo: Updated {pIdentifierCaseMap.Count} identifier cases")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierCaseMap error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Initializes the keyword case map with VB.NET keywords
        ''' </summary>
        Private Sub InitializeKeywordCaseMap()
            pKeywordCaseMap = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"AddHandler", "AddHandler"}, {"AddressOf", "AddressOf"}, {"Alias", "Alias"}, 
                {"and", "and"}, {"AndAlso", "AndAlso"}, {"As", "As"}, {"Boolean", "Boolean"}, 
                {"ByRef", "ByRef"}, {"Byte", "Byte"}, {"ByVal", "ByVal"}, {"Call", "Call"}, 
                {"Case", "Case"}, {"Catch", "Catch"}, {"CBool", "CBool"}, {"CByte", "CByte"}, 
                {"CChar", "CChar"}, {"CDate", "CDate"}, {"CDbl", "CDbl"}, {"CDec", "CDec"}, 
                {"Char", "Char"}, {"CInt", "CInt"}, {"Class", "Class"}, {"CLng", "CLng"}, 
                {"CObj", "CObj"}, {"Const", "Const"}, {"Continue", "Continue"}, 
                {"CSByte", "CSByte"}, {"CShort", "CShort"}, {"CSng", "CSng"}, {"CStr", "CStr"}, 
                {"CType", "CType"}, {"CUInt", "CUInt"}, {"CULng", "CULng"}, 
                {"CUShort", "CUShort"}, {"Date", "Date"}, {"Decimal", "Decimal"}, 
                {"Declare", "Declare"}, {"Default", "Default"}, {"Delegate", "Delegate"}, 
                {"Dim", "Dim"}, {"DirectCast", "DirectCast"}, {"Do", "Do"}, {"Double", "Double"}, 
                {"each", "each"}, {"Else", "Else"}, {"ElseIf", "ElseIf"}, {"End", "End"}, 
                {"Enum", "Enum"}, {"Erase", "Erase"}, {"error", "error"}, {"Event", "Event"}, 
                {"Exit", "Exit"}, {"False", "False"}, {"Finally", "Finally"}, {"For", "For"},
                {"Friend", "Friend"}, {"Function", "Function"}, {"Get", "Get"}, 
                {"GetType", "GetType"}, {"GetXMLNamespace", "GetXMLNamespace"}, 
                {"Global", "Global"}, {"GoSub", "GoSub"}, {"GoTo", "GoTo"}, {"Handles", "Handles"}, 
                {"If", "If"}, {"Implements", "Implements"}, {"Imports", "Imports"}, {"in", "in"}, 
                {"Inherits", "Inherits"}, {"Integer", "Integer"}, {"Interface", "Interface"}, 
                {"Is", "Is"}, {"IsNot", "IsNot"}, {"Let", "Let"}, {"Lib", "Lib"}, 
                {"Like", "Like"}, {"Long", "Long"}, {"Loop", "Loop"}, {"Me", "Me"}, 
                {"Mod", "Mod"}, {"Module", "Module"}, {"MustInherit", "MustInherit"}, 
                {"MustOverride", "MustOverride"}, {"MyBase", "MyBase"}, {"MyClass", "MyClass"}, 
                {"Namespace", "Namespace"}, {"Narrowing", "Narrowing"}, {"New", "New"}, 
                {"Next", "Next"}, {"Not", "Not"}, {"Nothing", "Nothing"}, 
                {"NotInheritable", "NotInheritable"}, {"NotOverridable", "NotOverridable"}, 
                {"Object", "Object"}, {"Of", "Of"}, {"On", "On"}, {"Operator", "Operator"}, 
                {"Option", "Option"}, {"Optional", "Optional"}, {"Or", "Or"}, {"OrElse", "OrElse"}, 
                {"Overloads", "Overloads"}, {"Overridable", "Overridable"}, 
                {"Overrides", "Overrides"}, {"ParamArray", "ParamArray"}, {"Partial", "Partial"}, 
                {"Private", "Private"}, {"Property", "Property"}, {"Protected", "Protected"}, 
                {"Public", "Public"}, {"RaiseEvent", "RaiseEvent"}, {"ReadOnly", "ReadOnly"}, 
                {"ReDim", "ReDim"}, {"REM", "REM"}, {"RemoveHandler", "RemoveHandler"}, 
                {"Resume", "Resume"}, {"Return", "Return"}, {"SByte", "SByte"}, 
                {"Select", "Select"}, {"Set", "Set"}, {"Shadows", "Shadows"}, 
                {"Shared", "Shared"}, {"Short", "Short"}, {"Single", "Single"}, 
                {"Static", "Static"}, {"Step", "Step"}, {"Stop", "Stop"}, {"String", "String"}, 
                {"Structure", "Structure"}, {"Sub", "Sub"}, {"SyncLock", "SyncLock"}, 
                {"Then", "Then"}, {"Throw", "Throw"}, {"To", "To"}, {"True", "True"}, 
                {"Try", "Try"}, {"TryCast", "TryCast"}, {"TypeOf", "TypeOf"}, {"UInteger", "UInteger"}, 
                {"ULong", "ULong"}, {"UShort", "UShort"}, {"Using", "Using"}, {"Variant", "Variant"}, 
                {"Wend", "Wend"}, {"When", "When"}, {"While", "While"}, {"Widening", "Widening"}, 
                {"with", "with"}, {"WithEvents", "WithEvents"}, {"WriteOnly", "WriteOnly"}, 
                {"Xor", "Xor"}
            }
        End Sub
        
        ''' <summary>
        ''' Simple word splitter that preserves positions
        ''' </summary>
        Private Function SplitIntoWords(vText As String) As String()
            Dim lWords As New List(Of String)
            Dim lCurrentWord As New StringBuilder()
            
            for each lChar in vText
                If Char.IsLetterOrDigit(lChar) OrElse lChar = "_"c Then
                    lCurrentWord.Append(lChar)
                Else
                    If lCurrentWord.Length > 0 Then
                        lWords.Add(lCurrentWord.ToString())
                        lCurrentWord.Clear()
                    End If
                End If
            Next
            
            If lCurrentWord.Length > 0 Then
                lWords.Add(lCurrentWord.ToString())
            End If
            
            Return lWords.ToArray()
        End Function

        ''' <summary>
        ''' Detects the text encoding of a byte array
        ''' </summary>
        ''' <param name="vBytes">The byte array to analyze</param>
        ''' <returns>The detected encoding (defaults to UTF8)</returns>
        ''' <remarks>
        ''' Checks for BOM markers to detect UTF-8, UTF-16, and UTF-32
        ''' </remarks>
        Private Function DetectEncoding(vBytes As Byte()) As Encoding
            Try
                If vBytes Is Nothing OrElse vBytes.Length = 0 Then
                    Return Encoding.UTF8
                End If
                
                ' Check for BOM (Byte Order Mark)
                If vBytes.Length >= 3 Then
                    ' UTF-8 BOM: EF BB BF
                    If vBytes(0) = &HEF AndAlso vBytes(1) = &HBB AndAlso vBytes(2) = &HBF Then
                        Return Encoding.UTF8
                    End If
                End If
                
                If vBytes.Length >= 2 Then
                    ' UTF-16 Little Endian: FF FE
                    If vBytes(0) = &HFF AndAlso vBytes(1) = &HFE Then
                        Return Encoding.Unicode
                    End If
                    
                    ' UTF-16 Big Endian: FE FF
                    If vBytes(0) = &HFE AndAlso vBytes(1) = &HFF Then
                        Return Encoding.BigEndianUnicode
                    End If
                End If
                
                If vBytes.Length >= 4 Then
                    ' UTF-32 Little Endian: FF FE 00 00
                    If vBytes(0) = &HFF AndAlso vBytes(1) = &HFE AndAlso vBytes(2) = 0 AndAlso vBytes(3) = 0 Then
                        Return Encoding.UTF32
                    End If
                End If
                
                ' Default to UTF-8 if no BOM detected
                Return Encoding.UTF8
                
            Catch ex As Exception
                Console.WriteLine($"DetectEncoding error: {ex.Message}")
                Return Encoding.UTF8
            End Try
        End Function
        
        ''' <summary>
        ''' Updates the TextLines collection from the Content property
        ''' </summary>
        ''' <remarks>
        ''' Splits Content into lines and ensures at least one line exists
        ''' </remarks>
        Private Sub UpdateTextLines()
            Try
                ' Split content into lines
                If Not String.IsNullOrEmpty(Content) Then
                    Dim lLines As String() = Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                    pTextLines = New List(Of String)(lLines)
                Else
                    pTextLines = New List(Of String)()
                End If
                
                ' Ensure at least one line exists
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
                
               ' Console.WriteLine($"UpdateTextLines: Split into {pTextLines.Count} lines")
                
            Catch ex As Exception
                Console.WriteLine($"UpdateTextLines error: {ex.Message}")
                ' Ensure we have at least something
                If pTextLines Is Nothing Then
                    pTextLines = New List(Of String)()
                End If
                If pTextLines.Count = 0 Then
                    pTextLines.Add("")
                End If
            End Try
        End Sub



        ''' <summary>
        ''' Forces immediate synchronous parsing and coloring for specified lines
        ''' </summary>
        ''' <param name="vStartLine">First line to parse (0-based)</param>
        ''' <param name="vEndLine">Last line to parse (0-based)</param>
        ''' <remarks>
        ''' This method is used for paste operations to ensure immediate syntax coloring.
        ''' Unlike async parsing, this runs synchronously to provide immediate visual feedback.
        ''' All parsing and coloring is handled internally within SourceFileInfo.
        ''' </remarks>
        Public Sub ForceImmediateParsing(vStartLine As Integer, vEndLine As Integer)
            Try
                Console.WriteLine($"SourceFileInfo.ForceImmediateParsing: Parsing lines {vStartLine} to {vEndLine}")
                
                ' Ensure we have valid line indices
                vStartLine = Math.Max(0, vStartLine)
                vEndLine = Math.Min(vEndLine, TextLines.Count - 1)
                
                If vStartLine > vEndLine Then Return
                
                EnsureLineMetadata()
                
                ' Parse and color each line in the range
                Dim lTokenCount As Integer = 0
                for lineIdx As Integer = vStartLine To vEndLine
                    Try
                        Dim lLineText As String = TextLines(lineIdx)
                        
                        ' Parse the line to get tokens with colors
                        Dim lTokens As List(Of SyntaxToken) = ParseLine(lLineText, lineIdx)
                        
                        If lTokens IsNot Nothing AndAlso lTokens.Count > 0 Then
                            lTokenCount += lTokens.Count
                            
                            ' Update LineMetadata with tokens
                            If LineMetadata IsNot Nothing AndAlso lineIdx < LineMetadata.Length Then
                                If LineMetadata(lineIdx) Is Nothing Then
                                    LineMetadata(lineIdx) = New LineMetadata()
                                End If
                                LineMetadata(lineIdx).SyntaxTokens = lTokens
                                LineMetadata(lineIdx).UpdateHash(lLineText)
                                LineMetadata(lineIdx).ParseState = LineParseState.eParsed
                            End If
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"ForceImmediateParsing line {lineIdx} error: {ex.Message}")
                    End Try
                Next
                
                Console.WriteLine($"SourceFileInfo.ForceImmediateParsing: Parsed {lTokenCount} tokens in {vEndLine - vStartLine + 1} lines")
                
                ' Notify that rendering has changed
                NotifyRenderingChanged(vStartLine, vEndLine)
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ForceImmediateParsing error: {ex.Message}")
            End Try
        End Sub
        



        ''' <summary>
        ''' Parses a single line of text and returns syntax tokens with colors
        ''' </summary>
        ''' <param name="vLineText">The text of the line to parse</param>
        ''' <param name="vLineIndex">The zero-based index of the line</param>
        ''' <returns>List of syntax tokens with color information</returns>
        ''' <remarks>
        ''' This method is used for immediate syntax highlighting during paste operations.
        ''' It works with the ProjectManager to tokenize the line and apply theme colors.
        ''' </remarks>
        Public Function ParseLine(vLineText As String, vLineIndex As Integer) As List(Of SyntaxToken)
            Try
                Dim lTokens As New List(Of SyntaxToken)()
                
                If String.IsNullOrEmpty(vLineText) Then Return lTokens
                
                ' Ensure we have a ProjectManager
                If pProjectManager Is Nothing Then
                    ' Try to get it
                    Dim lArgs As New ProjectManagerRequestEventArgs()
                    RaiseEvent ProjectManagerRequested(Me, lArgs)
                    If lArgs.HasProjectManager Then
                        pProjectManager = lArgs.ProjectManager
                    End If
                End If
                
                ' If still no ProjectManager, return empty
                If pProjectManager Is Nothing Then
                    Console.WriteLine($"ParseLine: No ProjectManager available")
                    Return lTokens
                End If
                
                ' Use VBTokenizer to tokenize the line
                Dim lTokenizer As New Syntax.VBTokenizer()
                Dim lRawTokens As List(Of Syntax.Token) = lTokenizer.TokenizeLine(vLineText)
                
                ' Convert Token to SyntaxToken with colors
                for each lRawToken in lRawTokens
                    ' Map token type
                    Dim lSyntaxType As SyntaxTokenType = MapTokenTypeToSyntaxType(lRawToken.Type) 
                   
                    ' Create SyntaxToken with color
                    Dim lSyntaxToken As New SyntaxToken(
                        lRawToken.StartColumn,
                        lRawToken.EndColumn - lRawToken.StartColumn + 1,
                        lSyntaxType
                    )
                    
                    lTokens.Add(lSyntaxToken)
                Next
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"ParseLine error: {ex.Message}")
                Return New List(Of SyntaxToken)()
            End Try
        End Function
        
        ''' <summary>
        ''' Maps a TokenType to a SyntaxTokenType
        ''' </summary>
        ''' <param name="vTokenType">The token type to map</param>
        ''' <returns>The corresponding syntax token type</returns>
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
        ''' Ensures character tokens array is properly sized for the current line count
        ''' </summary>
        Public Sub EnsureCharacterTokens()
            Try
                Dim lLineCount As Integer = If(TextLines?.Count, 0)
                If lLineCount = 0 Then Return
                
                ' Resize character tokens array if needed
                If pCharacterTokens Is Nothing OrElse pCharacterTokens.Length <> lLineCount Then
                    ReDim Preserve pCharacterTokens(lLineCount - 1)
                End If
                
                ' Ensure each line has proper token array
                For i As Integer = 0 To lLineCount - 1
                    Dim lLineLength As Integer = If(TextLines(i)?.Length, 0)
                    
                    If pCharacterTokens(i) Is Nothing OrElse pCharacterTokens(i).Length <> lLineLength Then
                        If lLineLength > 0 Then
                            ReDim pCharacterTokens(i)(lLineLength - 1)
                            ' Initialize with default normal token type
                            For j As Integer = 0 To lLineLength - 1
                                pCharacterTokens(i)(j) = CharacterToken.CreateDefault()
                            Next
                        Else
                            pCharacterTokens(i) = New Byte() {}
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"EnsureCharacterTokens error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Updates character tokens for a single line based on syntax tokens
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        ''' <param name="vTokens">List of syntax tokens for the line</param>
        ''' <remarks>
        ''' This stores only token types, not actual color strings. The editor will
        ''' look up colors at render time.
        ''' </remarks>
        Public Sub UpdateCharacterTokens(vLineIndex As Integer, vTokens As List(Of SyntaxToken))
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= pTextLines.Count Then Return
                If pCharacterTokens Is Nothing OrElse vLineIndex >= pCharacterTokens.Length Then Return
                
                Dim lLineText As String = pTextLines(vLineIndex)
                Dim lLineLength As Integer = lLineText.Length
                
                ' Ensure character token array is properly sized
                If pCharacterTokens(vLineIndex) Is Nothing OrElse pCharacterTokens(vLineIndex).Length <> lLineLength Then
                    If lLineLength > 0 Then
                        ReDim pCharacterTokens(vLineIndex)(lLineLength - 1)
                    Else
                        pCharacterTokens(vLineIndex) = New Byte() {}
                        Return
                    End If
                End If
                
                ' First, set all to default token type
                Dim lDefaultToken As Byte = CharacterToken.EncodeType(SyntaxTokenType.eNormal)
                For i As Integer = 0 To lLineLength - 1
                    pCharacterTokens(vLineIndex)(i) = lDefaultToken
                Next
                
                ' Apply token types from syntax tokens
                If vTokens IsNot Nothing Then
                    For Each lToken In vTokens
                        ' Ensure we don't go out of bounds
                        Dim lStartCol As Integer = Math.Max(0, lToken.StartColumn)
                        Dim lEndCol As Integer = Math.Min(lLineLength - 1, lToken.StartColumn + lToken.Length - 1)
                        
                        ' Encode the token with its style information
                        Dim lEncodedToken As Byte = CharacterToken.Encode(
                            lToken.TokenType, 
                            lToken.IsBold, 
                            lToken.IsItalic)
                        
                        ' Apply to character range
                        For k As Integer = lStartCol To lEndCol
                            If k >= 0 AndAlso k < pCharacterTokens(vLineIndex).Length Then
                                pCharacterTokens(vLineIndex)(k) = lEncodedToken
                            End If
                        Next
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateCharacterTokens error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Applies default token types to a line (used when no syntax tokens available)
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        Private Sub ApplyDefaultTokens(vLineIndex As Integer)
            Try
                ' Validate line index
                If vLineIndex < 0 OrElse vLineIndex >= pTextLines.Count Then Return
                
                Dim lLineText As String = pTextLines(vLineIndex)
                Dim lLineLength As Integer = lLineText.Length
                Dim lDefaultToken As Byte = CharacterToken.CreateDefault()
                
                ' Ensure CharacterTokens for this line
                If vLineIndex >= pCharacterTokens.Length Then
                    ReDim Preserve pCharacterTokens(pTextLines.Count - 1)
                End If
                
                If pCharacterTokens(vLineIndex) Is Nothing OrElse pCharacterTokens(vLineIndex).Length <> lLineLength Then
                    If lLineLength > 0 Then
                        ReDim pCharacterTokens(vLineIndex)(lLineLength - 1)
                    Else
                        pCharacterTokens(vLineIndex) = New Byte() {}
                        Return
                    End If
                End If
                
                ' Set all characters to default token
                For i As Integer = 0 To lLineLength - 1
                    pCharacterTokens(vLineIndex)(i) = lDefaultToken
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ApplyDefaultTokens error: {ex.Message}")
            End Try
        End Sub
    
    End Class
    
End Namespace
