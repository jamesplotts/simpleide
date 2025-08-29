 
' ProjectParser.vb - Parses entire project structure into unified SyntaxNode tree
' Created: 2025-08-25

Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Managers
    
    ''' <summary>
    ''' Parses an entire VB.NET project into a unified SyntaxNode tree structure
    ''' </summary>
    Public Class ProjectParser
        
        ' ===== Private Fields =====
        Private pProjectManager As ProjectManager
        Private pRootNode As SyntaxNode
        Private pRootNamespace As SyntaxNode
        Private pPartialClasses As Dictionary(Of String, SyntaxNode)
        Private pParseErrors As List(Of String)
        Private pCurrentFile As String
        Private pCurrentLineNumber As Integer
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new ProjectParser instance
        ''' </summary>
        ''' <param name="vProjectManager">The ProjectManager containing the source files</param>
        Public Sub New(vProjectManager As ProjectManager)
            If vProjectManager Is Nothing Then
                Throw New ArgumentNullException(NameOf(vProjectManager))
            End If
            
            pProjectManager = vProjectManager
            pPartialClasses = New Dictionary(Of String, SyntaxNode)(StringComparer.OrdinalIgnoreCase)
            pParseErrors = New List(Of String)()
        End Sub
        
        ' ===== Public Methods =====
        
        ' Replace: SimpleIDE.Managers.ProjectParser.ParseProject
        ''' <summary>
        ''' Parses all source files in the project into a unified syntax tree
        ''' </summary>
        ''' <returns>The root SyntaxNode of the project tree</returns>
        Public Function ParseProject() As SyntaxNode
            Try
                Console.WriteLine("ProjectParser: Starting project parse...")
                
                ' Initialize the project structure
                InitializeProjectStructure()
                
                ' Get all source files from ProjectManager
                Dim lSourceFiles As Dictionary(Of String, SourceFileInfo) = pProjectManager.SourceFiles
                
                Console.WriteLine($"ProjectParser: Retrieved {If(lSourceFiles?.Count, 0)} source files from ProjectManager")
                
                If lSourceFiles Is Nothing OrElse lSourceFiles.Count = 0 Then
                    Console.WriteLine("ProjectParser: No source files to parse")
                    Console.WriteLine("  Checking if files are loaded...")
                    
                    ' Try to get files directly
                    If pProjectManager.CurrentProjectInfo IsNot Nothing Then
                        Dim lProjectFiles As List(Of String) = pProjectManager.CurrentProjectInfo.SourceFiles
                        Console.WriteLine($"  Project has {lProjectFiles.Count} file paths")
                        for each lPath in lProjectFiles.Take(5)
                            Console.WriteLine($"    - {Path.GetFileName(lPath)}")
                        Next
                    End If
                    
                    Return pRootNode
                End If
                
                ' Check the first few files to see if they have content
                Console.WriteLine("ProjectParser: Checking file content loading...")
                Dim lFilesWithContent As Integer = 0
                Dim lFilesWithoutContent As Integer = 0
                
                for each lFileEntry in lSourceFiles.Take(10)
                    Dim lSourceFile As SourceFileInfo = lFileEntry.Value
                    If lSourceFile.IsLoaded AndAlso lSourceFile.TextLines IsNot Nothing AndAlso lSourceFile.TextLines.Count > 0 Then
                        lFilesWithContent += 1
                        Console.WriteLine($"  ✓ {Path.GetFileName(lFileEntry.Key)}: {lSourceFile.TextLines.Count} lines")
                    Else
                        lFilesWithoutContent += 1
                        Console.WriteLine($"  ✗ {Path.GetFileName(lFileEntry.Key)}: Not loaded or no lines")
                        ' Try to debug this file
                        lSourceFile.DebugContent()
                    End If
                Next
                
                Console.WriteLine($"ProjectParser: Files with content: {lFilesWithContent}, without: {lFilesWithoutContent}")
                
                ' Sort files alphabetically for consistent processing
                Dim lSortedFiles As New List(Of KeyValuePair(Of String, SourceFileInfo))(lSourceFiles)
                lSortedFiles.Sort(Function(a, b) String.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase))
                
                ' Log first few files to be parsed
                Console.WriteLine("ProjectParser: Files to parse:")
                for each lFile in lSortedFiles.Take(5)
                    Console.WriteLine($"  - {Path.GetFileName(lFile.Key)} (Loaded: {lFile.Value.IsLoaded}, Lines: {If(lFile.Value.TextLines?.Count, 0)})")
                Next
                
                ' Parse each file
                Dim lFilesProcessed As Integer = 0
                for each lFileEntry in lSortedFiles
                    Dim lSourceFile As SourceFileInfo = lFileEntry.Value
                    
                    ' Ensure file is loaded
                    If Not lSourceFile.IsLoaded Then
                        Console.WriteLine($"ProjectParser: Loading {Path.GetFileName(lSourceFile.FilePath)}")
                        lSourceFile.LoadContent()
                    End If
                    
                    ' Check if we have TextLines
                    If lSourceFile.TextLines Is Nothing OrElse lSourceFile.TextLines.Count = 0 Then
                        Console.WriteLine($"ProjectParser: WARNING - {Path.GetFileName(lSourceFile.FilePath)} has no TextLines!")
                        Continue for
                    End If
                    
                    ' Parse the file
                    ParseSourceFile(lSourceFile)
                    lFilesProcessed += 1
                    
                    ' Log progress every 10 files
                    If lFilesProcessed Mod 10 = 0 Then
                        Console.WriteLine($"ProjectParser: Processed {lFilesProcessed}/{lSortedFiles.Count} files")
                    End If
                Next
                
                ' Merge partial classes
                Console.WriteLine($"ProjectParser: Merging {pPartialClasses.Count} partial classes")
                MergePartialClasses()
                
                ' Log final structure
                Console.WriteLine($"ProjectParser: Parse complete. Total nodes: {CountNodes(pRootNode)}")
                Console.WriteLine($"ProjectParser: Root structure:")
                LogNodeStructure(pRootNode, 0, 3) ' Log up to 3 levels deep
                
                If pParseErrors.Count > 0 Then
                    Console.WriteLine($"ProjectParser: {pParseErrors.Count} errors encountered during parsing")
                    for each lError in pParseErrors.Take(5)
                        Console.WriteLine($"  Error: {lError}")
                    Next
                End If
                
                Return pRootNode
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseProject error: {ex.Message}")
                Console.WriteLine($"  Stack trace: {ex.StackTrace}")
                LogError($"Fatal error during project parse: {ex.Message}")
                Return pRootNode
            End Try
        End Function


        

        ''' <summary>
        ''' Merges all partial classes that were collected during parsing
        ''' </summary>
        Private Sub MergePartialClasses()
            Try
                Console.WriteLine($"MergePartialClasses: Processing {pPartialClasses.Count} Partial Class entries")
                
                ' Group partial classes by their key (ParentPath:TypeName)
                Dim lProcessedKeys As New HashSet(Of String)()
                
                for each lEntry in pPartialClasses
                    Dim lKey As String = lEntry.Key
                    
                    ' Skip if already processed
                    If lProcessedKeys.Contains(lKey) Then Continue for
                    
                    ' Mark as processed
                    lProcessedKeys.Add(lKey)
                    
                    Dim lMainClass As SyntaxNode = lEntry.Value
                    
                    ' Log the merge
                    If lMainClass.IsPartial Then
                        Dim lFileCount As Integer = 1
                        If lMainClass.Attributes IsNot Nothing AndAlso lMainClass.Attributes.ContainsKey("FilePaths") Then
                            lFileCount = lMainClass.Attributes("FilePaths").Split(";"c).Length
                        End If
                        Console.WriteLine($"  Partial Class '{lMainClass.Name}' merged from {lFileCount} files")
                    End If
                Next
                
                Console.WriteLine($"MergePartialClasses: Complete")
                
            Catch ex As Exception
                Console.WriteLine($"MergePartialClasses error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Helper method to log node structure for debugging
        ''' </summary>
        Private Sub LogNodeStructure(vNode As SyntaxNode, vIndent As Integer, vMaxDepth As Integer)
            Try
                If vNode Is Nothing OrElse vIndent >= vMaxDepth Then Return
                
                Dim lIndentStr As String = New String(" "c, vIndent * 2)
                Console.WriteLine($"{lIndentStr}{vNode.Name} ({vNode.NodeType}) - {vNode.Children.Count} children")
                
                ' Log first few children
                for each lChild in vNode.Children.Take(3)
                    LogNodeStructure(lChild, vIndent + 1, vMaxDepth)
                Next
                
                If vNode.Children.Count > 3 Then
                    Console.WriteLine($"{lIndentStr}  ... and {vNode.Children.Count - 3} more")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LogNodeStructure error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Parses a single file's content and returns a ParseResult
        ''' </summary>
        ''' <param name="vContent">The content to parse</param>
        ''' <param name="vRootNamespace">The root namespace for the project</param>
        ''' <param name="vFilePath">The file path being parsed</param>
        ''' <returns>A ParseResult containing the parsed structure</returns>
        Public Function ParseContent(vContent As String, vRootNamespace As String, vFilePath As String) As ParseResult
            Try
                Console.WriteLine($"ProjectParser.ParseContent: Parsing content for {vFilePath}")
                
                ' Create a temporary SourceFileInfo for parsing
                Dim lTempFile As New SourceFileInfo(vFilePath, System.IO.Path.GetDirectoryName(vFilePath))
                lTempFile.Content = vContent
                lTempFile.IsLoaded = True
                
                ' Initialize if needed (for single file parsing)
                If pRootNode Is Nothing Then
                    InitializeProjectStructure()
                End If
                
                ' Store current file
                pCurrentFile = vFilePath
                pCurrentLineNumber = 0
                
                ' Create a temporary root for this single file
                Dim lTempRoot As New SyntaxNode(CodeNodeType.eProject, "TempProject")
                Dim lTempNamespace As New SyntaxNode(CodeNodeType.eNamespace, vRootNamespace)
                lTempNamespace.IsImplicit = True
                lTempRoot.AddChild(lTempNamespace)
                
                ' Store original values
                Dim lOriginalRoot As SyntaxNode = pRootNode
                Dim lOriginalNamespace As SyntaxNode = pRootNamespace
                
                ' Temporarily use the temp structures
                pRootNode = lTempRoot
                pRootNamespace = lTempNamespace
                
                ' Parse the file
                ParseSourceFile(lTempFile)
                
                ' Create result
                Dim lResult As New ParseResult()
                lResult.RootNode = lTempRoot
                
                ' Restore original values
                pRootNode = lOriginalRoot
                pRootNamespace = lOriginalNamespace
                
                Console.WriteLine($"ProjectParser.ParseContent: Completed parsing {vFilePath}")
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseContent error: {ex.Message}")
                Return New ParseResult() ' Return empty result on error
            End Try
        End Function

        
        ''' <summary>
        ''' Gets the list of parse errors encountered
        ''' </summary>
        Public Function GetParseErrors() As List(Of String)
            Return New List(Of String)(pParseErrors)
        End Function
        
        ' ===== Private Methods - Initialization =====
        
        ''' <summary>
        ''' Initializes the project root and namespace structure
        ''' </summary>
        Private Sub InitializeProjectStructure()
            Try
                ' Create project root node
                pRootNode = New SyntaxNode(CodeNodeType.eDocument, "Project")
                pRootNode.FilePath = pProjectManager.CurrentProjectPath
                pRootNode.StartLine = 0
                pRootNode.EndLine = Integer.MaxValue
                
                ' Get root namespace from project
                Dim lRootNamespaceName As String = ""
                If pProjectManager.CurrentProjectInfo IsNot Nothing Then
                    lRootNamespaceName = pProjectManager.CurrentProjectInfo.GetEffectiveRootNamespace()
                End If
                
                If String.IsNullOrEmpty(lRootNamespaceName) Then
                    lRootNamespaceName = "SimpleIDE" ' Default fallback
                End If
                
                ' Create root namespace node
                pRootNamespace = New SyntaxNode(CodeNodeType.eNamespace, lRootNamespaceName)
                pRootNamespace.IsImplicit = True
                pRootNamespace.StartLine = 0
                pRootNamespace.EndLine = Integer.MaxValue
                
                ' Add root namespace to project
                pRootNode.AddChild(pRootNamespace)
                
                Console.WriteLine($"ProjectParser: Initialized with root Namespace '{lRootNamespaceName}'")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.InitializeProjectStructure error: {ex.Message}")
                LogError($"Failed To initialize project Structure: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods - File Parsing =====
        
        ''' <summary>
        ''' Parses a single source file into the project structure with strict validation
        ''' </summary>
        ''' <param name="vSourceFile">The source file to parse</param>
        Private Sub ParseSourceFile(vSourceFile As SourceFileInfo)
            Try
                If vSourceFile Is Nothing Then Return
                If Not vSourceFile.IsLoaded Then vSourceFile.LoadContent()
                
                pCurrentFile = vSourceFile.FilePath
                
                ' Parse the file into the project structure
                ParseFileContent(pCurrentFile, vSourceFile)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseSourceFile error in {pCurrentFile}: {ex.Message}")
                pParseErrors.Add($"Error parsing {pCurrentFile}: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks if a line is a method declaration
        ''' </summary>
        Private Function IsMethodDeclaration(vLine As String) As Boolean
            Try
                Return Regex.IsMatch(vLine, "^\s*(Public|Private|Protected|Friend|Shared|Overrides|Overridable|MustOverride|NotOverridable|Shadows)*\s*(Sub|Function)\s+\w+", RegexOptions.IgnoreCase)
            Catch ex As Exception
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Checks if a line is a property declaration
        ''' </summary>
        Private Function IsPropertyDeclaration(vLine As String) As Boolean
            Try
                Return Regex.IsMatch(vLine, "^\s*(Public|Private|Protected|Friend|Shared|Overrides|Overridable|MustOverride|NotOverridable|ReadOnly|WriteOnly|Default|Shadows)*\s*Property\s+\w+", RegexOptions.IgnoreCase)
            Catch ex As Exception
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Checks if a line is an event declaration
        ''' </summary>
        Private Function IsEventDeclaration(vLine As String) As Boolean
            Try
                Return Regex.IsMatch(vLine, "^\s*(Public|Private|Protected|Friend|Shared|Shadows)*\s*Event\s+\w+", RegexOptions.IgnoreCase)
            Catch ex As Exception
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Checks if a line is a field declaration
        ''' </summary>
        Private Function IsFieldDeclaration(vLine As String) As Boolean
            Try
                ' Must start with visibility modifier or Dim/Const
                If Not Regex.IsMatch(vLine, "^\s*(Public|Private|Protected|Friend|Dim|Const|Shared|ReadOnly|WithEvents)", RegexOptions.IgnoreCase) Then
                    Return False
                End If
                
                ' Must not be a method, property, or event
                If IsMethodDeclaration(vLine) OrElse IsPropertyDeclaration(vLine) OrElse IsEventDeclaration(vLine) Then
                    Return False
                End If
                
                ' Must have a valid identifier
                Return Regex.IsMatch(vLine, "\b\w+\s*(As\s+|\=)", RegexOptions.IgnoreCase)
                
            Catch ex As Exception
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Checks if a line is an End Sub or End Function statement
        ''' </summary>
        Private Function IsEndMethodStatement(vLine As String) As Boolean
            Try
                Return Regex.IsMatch(vLine, "^\s*End\s+(Sub|Function)\b", RegexOptions.IgnoreCase)
            Catch ex As Exception
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Checks if a line starts a nested block
        ''' </summary>
        Private Function IsNestingStatement(vLine As String) As Boolean
            Try
                ' Check for block start statements
                If Regex.IsMatch(vLine, "^\s*(If|For|While|Do|Select|Try|Using|SyncLock|With)\b", RegexOptions.IgnoreCase) Then
                    ' Make sure it's not a single-line If
                    If Not Regex.IsMatch(vLine, "\bThen\b.*\bEnd If\b", RegexOptions.IgnoreCase) Then
                        Return True
                    End If
                End If
                Return False
            Catch ex As Exception
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Validates if a string is a valid namespace name
        ''' </summary>
        Private Function IsValidNamespace(vName As String) As Boolean
            Try
                If String.IsNullOrWhiteSpace(vName) Then Return False
                
                ' Must not be a keyword or fragment
                Dim lInvalidNames As String() = {"imports", "Imports", "AndAlso", "OrElse", "Narrowing", 
                                                 "Widening", "GetValues", "ToString", "GetType", "vNamespace",
                                                 "statements", "namespace-statement", "all"}
                
                If lInvalidNames.Contains(vName) Then
                    Return False
                End If
                
                ' Check for method call patterns
                If vName.Contains("(") OrElse vName.Contains(")") OrElse vName.Contains(".Get") OrElse 
                   vName.Contains(".ToString") OrElse vName.Contains("Bridge.") Then
                    Return False
                End If
                
                ' Must be valid identifier parts separated by dots
                Dim lParts As String() = vName.Split("."c)
                for each lPart in lParts
                    If Not IsValidIdentifier(lPart) Then
                        Return False
                    End If
                Next
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidNamespace error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Extracts a valid namespace name from a Namespace declaration line
        ''' </summary>
        Private Function ExtractNamespaceName(vLine As String) As String
            Try
                ' Remove "Namespace" keyword
                Dim lName As String = vLine.Substring(10).Trim()
                
                ' Remove any trailing comments
                Dim lCommentIndex As Integer = lName.IndexOf("'"c)
                If lCommentIndex >= 0 Then
                    lName = lName.Substring(0, lCommentIndex).Trim()
                End If
                
                ' Validate the name contains only valid characters
                If Not IsValidNamespace(lName) Then
                    Return ""
                End If
                
                Return lName
                
            Catch ex As Exception
                Console.WriteLine($"ExtractNamespaceName error: {ex.Message}")
                Return ""
            End Try
        End Function

        ' Replace: SimpleIDE.Managers.ProjectParser.ParseFileContent
        ''' <summary>
        ''' Parses file content and adds to project structure with strict validation
        ''' </summary>
        ''' <param name="vFilePath">Path to the file being parsed</param>
        ''' <param name="vSourceFile">Content of the file</param>
        Private Sub ParseFileContent(vFilePath As String, vSourceFile As SourceFileInfo)
            Try
                pCurrentFile = vFilePath
                Dim lLines As List(Of String) = vSourceFile.TextLines
                
                Console.WriteLine($"ParseFileContent: {Path.GetFileName(vFilePath)}")
                Console.WriteLine($"  Lines count: {lLines.Count}")
                Console.WriteLine($"  Content loaded: {vSourceFile.IsLoaded}")
                
                If lLines.Count = 0 Then
                    Console.WriteLine("  WARNING: File has no lines!")
                    Return
                End If
                
                ' Show first few non-empty, non-comment lines for debugging
                Dim lSampleLines As New List(Of String)()
                For Each lLine In lLines.Take(50)
                    Dim lTrimmed As String = lLine.Trim()
                    If Not String.IsNullOrWhiteSpace(lTrimmed) AndAlso Not lTrimmed.StartsWith("'") Then
                        lSampleLines.Add(lTrimmed)
                        If lSampleLines.Count >= 5 Then Exit for
                    End If
                Next
                
                If lSampleLines.Count > 0 Then
                    Console.WriteLine($"  First few lines:")
                    for each lLine in lSampleLines
                        Console.WriteLine($"    {lLine.Substring(0, Math.Min(60, lLine.Length))}")
                    Next
                End If
                
                ' Track current context
                Dim lCurrentNamespace As SyntaxNode = pRootNamespace
                Dim lNamespaceStack As New Stack(Of SyntaxNode)()
                Dim lTypeStack As New Stack(Of SyntaxNode)()
                Dim lCurrentType As SyntaxNode = Nothing
                Dim lInMethodBody As Boolean = False
                Dim lMethodNestLevel As Integer = 0
                
                Dim lTypesFound As Integer = 0
                
                Dim i As Integer = 0
                While i < lLines.Count
                    Dim lLine As String = lLines(i)
                    Dim lTrimmedLine As String = lLine.Trim()
                    pCurrentLineNumber = i
                    
                    ' Skip empty lines and any comments
                    If String.IsNullOrWhiteSpace(lTrimmedLine) OrElse lTrimmedLine.StartsWith("'") Then
                        i += 1
                        Continue While
                    End If
                    
                    ' Skip Imports statements - they're not nodes in the tree
                    If lTrimmedLine.StartsWith("Imports ", StringComparison.OrdinalIgnoreCase) Then
                        i += 1
                        Continue While
                    End If
                    
                    ' Skip Option statements
                    If lTrimmedLine.StartsWith("Option ", StringComparison.OrdinalIgnoreCase) Then
                        i += 1
                        Continue While
                    End If
                    
                    ' Check for namespace declaration - ONLY at file level
                    If lTypeStack.Count = 0 AndAlso lTrimmedLine.StartsWith("Namespace ", StringComparison.OrdinalIgnoreCase) Then
                        Dim lNamespaceName As String = ExtractNamespaceName(lTrimmedLine)
                        If Not String.IsNullOrEmpty(lNamespaceName) AndAlso IsValidNamespace(lNamespaceName) Then
                            ' Find or create namespace
                            lCurrentNamespace = FindOrCreateNamespace(lNamespaceName, pRootNamespace)
                            lNamespaceStack.Push(lCurrentNamespace)
                            Console.WriteLine($"  Entered Namespace: {lNamespaceName} at line {i}")
                        End If
                        i += 1
                        Continue While
                    End If
                    
                    ' Check for End Namespace
                    If lTrimmedLine.Equals("End Namespace", StringComparison.OrdinalIgnoreCase) Then
                        If lNamespaceStack.Count > 0 Then
                            lNamespaceStack.Pop()
                            lCurrentNamespace = If(lNamespaceStack.Count > 0, lNamespaceStack.Peek(), pRootNamespace)
                            Console.WriteLine($"  Exited Namespace at line {i}")
                        End If
                        i += 1
                        Continue While
                    End If
                    
                    ' If we're in a method body, skip until we find the end
                    If lInMethodBody Then
                        If IsEndMethodStatement(lTrimmedLine) Then
                            lInMethodBody = False
                            Console.WriteLine($"    End Of method at line {i}")
                        End If
                        i += 1
                        Continue While
                    End If
                    
                    ' Check for type declaration (Class, Module, Interface, Structure, Enum)
                    Dim lTypeNode As SyntaxNode = Nothing
                    
                    ' Debug: Check what we're looking at
                    If lTrimmedLine.Contains("Class ", StringComparison.OrdinalIgnoreCase) OrElse
                       lTrimmedLine.Contains("Module ", StringComparison.OrdinalIgnoreCase) OrElse
                       lTrimmedLine.Contains("Interface ", StringComparison.OrdinalIgnoreCase) OrElse
                       lTrimmedLine.Contains("Structure ", StringComparison.OrdinalIgnoreCase) Then
                        Console.WriteLine($"  Potential type declaration at line {i}: {lTrimmedLine.Substring(0, Math.Min(60, lTrimmedLine.Length))}")
                    End If
                    
                    If ParseTypeDeclaration(lTrimmedLine, lCurrentNamespace, lTypeNode, lLines, i) Then
                        If lTypeNode IsNot Nothing Then
                            lTypesFound += 1
                            Console.WriteLine($"  ✓ Found type: {lTypeNode.Name} ({lTypeNode.NodeType}) in Namespace {lCurrentNamespace.Name}")
                            lCurrentType = lTypeNode
                            lTypeStack.Push(lCurrentType)
                            ' Parse to the end of the type
                            i = ParseTypeBody(lLines, i + 1, lTypeNode)
                            lTypeStack.Pop()
                            lCurrentType = If(lTypeStack.Count > 0, lTypeStack.Peek(), Nothing)
                        Else
                            Console.WriteLine($"  WARNING: ParseTypeDeclaration returned True but no node at line {i}")
                        End If
                        i += 1
                        Continue While
                    End If
                    
                    ' Only parse members if we're inside a type
                    If lCurrentType IsNot Nothing Then
                        ' Check if this starts a method/function
                        If IsMethodDeclaration(lTrimmedLine) Then
                            If ParseMethodOrFunction(lTrimmedLine, lCurrentType, i) Then
                                lInMethodBody = True
                                Console.WriteLine($"    Started method at line {i}")
                            End If
                        ElseIf IsPropertyDeclaration(lTrimmedLine) Then
                            ParseProperty(lTrimmedLine, lCurrentType, i)
                        ElseIf IsEventDeclaration(lTrimmedLine) Then
                            ParseEvent(lTrimmedLine, lCurrentType, i, lLines)
                        ElseIf IsFieldDeclaration(lTrimmedLine) Then
                            ParseField(lTrimmedLine, lCurrentType, i, lLines)
                        End If
                    End If
                    
                    i += 1
                End While
                
                Console.WriteLine($"  ParseFileContent complete: Found {lTypesFound} types")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseFileContent error: {ex.Message}")
                pParseErrors.Add($"error in {vFilePath}: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Parses method/function declaration and creates node (modified to return node)
        ''' </summary>
        Private Function ParseMethodOrFunctionWithNode(vLine As String, vTypeNode As SyntaxNode, 
                                                      vLineNumber As Integer, ByRef vMethodNode As SyntaxNode) As Boolean
            Try
                ' Tokenize the line
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count < 2 Then Return False
                
                ' Find "Sub" or "Function" keyword
                Dim lMethodIndex As Integer = -1
                Dim lMethodType As String = ""
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Sub", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lTokens(i), "Function", StringComparison.OrdinalIgnoreCase) Then
                        lMethodIndex = i
                        lMethodType = lTokens(i)
                        Exit for
                    End If
                Next
                
                If lMethodIndex < 0 OrElse lMethodIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get method name (next token after "Sub" or "Function")
                Dim lName As String = lTokens(lMethodIndex + 1)
                
                ' Remove any parentheses from name
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                ' Create appropriate node type
                Dim lNodeType As CodeNodeType = If(lMethodType.Equals("Sub", StringComparison.OrdinalIgnoreCase),
                                                   CodeNodeType.eMethod, CodeNodeType.eFunction)
                
                ' Create the method node
                vMethodNode = New SyntaxNode(lNodeType, lName)
                vMethodNode.StartLine = vLineNumber
                vMethodNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, vMethodNode)
                
                ' Parse parameters
                ParseParameters(vLine, vMethodNode)
                
                ' Parse return type (for functions)
                If lNodeType = CodeNodeType.eFunction Then
                    ParseReturnType(vLine, vMethodNode)
                End If
                
                ' Add to parent
                vTypeNode.AddChild(vMethodNode)
                
                Console.WriteLine($"  Added {lMethodType}: {lName} To {vTypeNode.Name}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseMethodOrFunctionWithNode error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses property declaration and creates node (modified to return node)
        ''' </summary>
        Private Function ParsePropertyWithNode(vLine As String, vTypeNode As SyntaxNode, 
                                              vLineNumber As Integer, ByRef vPropertyNode As SyntaxNode) As Boolean
            Try
                ' Tokenize the line
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count < 2 Then Return False
                
                ' Find "Property" keyword
                Dim lPropertyIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Property", StringComparison.OrdinalIgnoreCase) Then
                        lPropertyIndex = i
                        Exit for
                    End If
                Next
                
                If lPropertyIndex < 0 OrElse lPropertyIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get property name (next token after "Property")
                Dim lName As String = lTokens(lPropertyIndex + 1)
                
                ' Remove any parentheses from name
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                ' Create property node
                vPropertyNode = New SyntaxNode(CodeNodeType.eProperty, lName)
                vPropertyNode.StartLine = vLineNumber
                vPropertyNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, vPropertyNode)
                
                ' Parse property type (return type)
                ParseReturnType(vLine, vPropertyNode)
                
                ' Check if it's auto-implemented (single line with no explicit Get/Set)
                If Not vLine.Contains(" Get") AndAlso Not vLine.Contains(" Set") Then
                    vPropertyNode.IsAutoImplemented = True
                End If
                
                ' Add to parent
                vTypeNode.AddChild(vPropertyNode)
                
                Console.WriteLine($"  Added Property: {lName} To {vTypeNode.Name}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParsePropertyWithNode error: {ex.Message}")
                Return False
            End Try
        End Function
        


        
        ''' <summary>
        ''' Parses a namespace declaration using proper tokenization
        ''' </summary>
        Private Function ParseNamespaceDeclaration(vLine As String, ByRef vCurrentNamespace As SyntaxNode, vStack As Stack(Of SyntaxNode)) As Boolean
            Try
                ' Tokenize the line
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                ' Check if first non-whitespace token is "Namespace"
                If lTokens.Count < 2 Then
                    Return False
                End If
                
                ' First token must be "Namespace" (case-insensitive)
                If Not lTokens(0).Equals("Namespace", StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If
                
                ' Rest of tokens form the namespace name (excluding comments)
                Dim lNamespaceName As New System.Text.StringBuilder()
                for i As Integer = 1 To lTokens.Count - 1
                    Dim lToken As String = lTokens(i)
                    ' Skip operators and separators
                    If lToken.Length = 1 AndAlso "(){}[]<>=+-/*:.".Contains(lToken) Then
                        Continue for
                    End If
                    If lNamespaceName.Length > 0 Then
                        lNamespaceName.Append(".")
                    End If
                    lNamespaceName.Append(lToken)
                Next
                
                If lNamespaceName.Length = 0 Then
                    Return False
                End If
                
                Dim lNamespacePath As String = lNamespaceName.ToString()
                
                ' Navigate to or create the namespace hierarchy
                Dim lTargetNamespace As SyntaxNode = FindOrCreateNamespace(lNamespacePath, pRootNamespace)
                
                ' Push current namespace to stack and update
                vStack.Push(vCurrentNamespace)
                vCurrentNamespace = lTargetNamespace
                
                Console.WriteLine($"ProjectParser: Entered Namespace '{lNamespacePath}'")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseNamespaceDeclaration error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Finds or creates a namespace node in the hierarchy
        ''' </summary>
        Private Function FindOrCreateNamespace(vPath As String, vRootNamespace As SyntaxNode) As SyntaxNode
            Try
                If String.IsNullOrEmpty(vPath) Then
                    Return vRootNamespace
                End If
                
                Dim lSegments As String() = vPath.Split("."c)
                Dim lCurrentParent As SyntaxNode = vRootNamespace
                
                for each lSegment in lSegments
                    If String.IsNullOrWhiteSpace(lSegment) Then
                        Continue for
                    End If
                    
                    ' Look for existing namespace child
                    Dim lFoundChild As SyntaxNode = Nothing
                    for each lChild in lCurrentParent.Children
                        If lChild.NodeType = CodeNodeType.eNamespace AndAlso
                           String.Equals(lChild.Name, lSegment, StringComparison.OrdinalIgnoreCase) Then
                            lFoundChild = lChild
                            Exit for
                        End If
                    Next
                    
                    If lFoundChild IsNot Nothing Then
                        lCurrentParent = lFoundChild
                    Else
                        ' Create new namespace node
                        Dim lNewNamespace As New SyntaxNode(CodeNodeType.eNamespace, lSegment)
                        lNewNamespace.FilePath = pCurrentFile
                        lNewNamespace.StartLine = pCurrentLineNumber
                        lCurrentParent.AddChild(lNewNamespace)
                        lCurrentParent = lNewNamespace
                    End If
                Next
                
                Return lCurrentParent
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.FindOrCreateNamespace error: {ex.Message}")
                Return vRootNamespace
            End Try
        End Function
        

        
        ''' <summary>
        ''' Handles partial type declarations, merging them properly
        ''' </summary>
        Private Function HandlePartialType(vParentNode As SyntaxNode, vTypeName As String, vNodeType As CodeNodeType, vFilePath As String) As SyntaxNode
            Try
                ' Build unique key for this partial type based on parent's fully qualified name
                Dim lKey As String = $"{vParentNode.GetFullyQualifiedName()}.{vTypeName}"
                
                ' Check if we already have this partial type in our dictionary
                Dim lPartialNode As SyntaxNode = Nothing
                If pPartialClasses.TryGetValue(lKey, lPartialNode) Then
                    ' Update the end line if this is a continuation
                    If pCurrentLineNumber > lPartialNode.EndLine Then
                        lPartialNode.EndLine = pCurrentLineNumber
                    End If
                    Console.WriteLine($"ProjectParser: Merging Partial type '{vTypeName}' in parent '{vParentNode.Name}'")
                    Return lPartialNode
                End If
                
                ' Check if a non-partial version already exists in the parent
                for each lChild in vParentNode.Children
                    If lChild.NodeType = vNodeType AndAlso
                       String.Equals(lChild.Name, vTypeName, StringComparison.OrdinalIgnoreCase) Then
                        ' Found existing node - mark as partial and use it
                        lChild.IsPartial = True
                        pPartialClasses(lKey) = lChild
                        Console.WriteLine($"ProjectParser: Converting existing type '{vTypeName}' to partial in parent '{vParentNode.Name}'")
                        Return lChild
                    End If
                Next
                
                ' Create new partial type node
                Dim lNewNode As New SyntaxNode(vNodeType, vTypeName)
                lNewNode.FilePath = vFilePath
                lNewNode.StartLine = pCurrentLineNumber
                lNewNode.IsPartial = True
                
                ' Add to parent (namespace or containing type for nested classes)
                vParentNode.AddChild(lNewNode)
                
                ' Register in partial classes dictionary
                pPartialClasses(lKey) = lNewNode
                
                Console.WriteLine($"ProjectParser: Created New Partial type '{vTypeName}' in parent '{vParentNode.Name}'")
                
                Return lNewNode
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.HandlePartialType error: {ex.Message}")
                ' Create a new node as fallback
                Dim lFallbackNode As New SyntaxNode(vNodeType, vTypeName)
                lFallbackNode.FilePath = vFilePath
                vParentNode.AddChild(lFallbackNode)
                Return lFallbackNode
            End Try
        End Function
        
        ' Replace: SimpleIDE.Managers.ProjectParser.ParseMember
        ''' <summary>
        ''' Parses member declarations within a type with XML documentation support
        ''' </summary>
        Private Sub ParseMember(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer, vLines As List(Of String))
            Try
                ' Skip if line is a comment (but not XML doc comments - they're handled with declarations)
                If vLine.TrimStart().StartsWith("'") AndAlso Not vLine.TrimStart().StartsWith("'''") Then
                    Return
                End If
                
                ' Skip local variable declarations inside methods
                If IsLocalVariableDeclaration(vLine) Then
                    Return
                End If
                
                ' Tokenize the line for keyword detection
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count = 0 Then Return
                
                ' Check for type declarations (nested types)
                Dim lTypeKeywords As String() = {"Class", "Module", "Interface", "Structure", "Struct", "Enum", "Delegate"}
                for each lKeyword in lTypeKeywords
                    for each lToken in lTokens
                        If String.Equals(lToken, lKeyword, StringComparison.OrdinalIgnoreCase) Then
                            ' Nested type - parse as type declaration
                            Dim lNestedType As SyntaxNode = Nothing
                            If ParseTypeDeclaration(vLine, vTypeNode, lNestedType, vLines, vLineNumber) Then
                                If lNestedType IsNot Nothing Then
                                    ExtractAndApplyXmlDocumentation(vLines, vLineNumber, lNestedType)
                                End If
                            End If
                            Return
                        End If
                    Next
                Next
                
                ' Check for Events
                If ParseEvent(vLine, vTypeNode, vLineNumber, vLines) Then
                    Return
                End If
                
                ' Check for Delegates (method-level)
                If ParseDelegate(vLine, vTypeNode, vLineNumber, vLines) Then
                    Return
                End If
                
                ' Check for Constants
                If ParseConstant(vLine, vTypeNode, vLineNumber, vLines) Then
                    Return
                End If
                
                ' Check for Fields
                If ParseField(vLine, vTypeNode, vLineNumber, vLines) Then
                    Return
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseMember error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks if a line is an End Namespace statement
        ''' </summary>
        ''' <param name="vLine">The line to check</param>
        ''' <returns>True if the line is "End Namespace", False otherwise</returns>
        Private Function IsEndNamespaceStatement(vLine As String) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                If lTokens.Count < 2 Then Return False
                
                ' Must be exactly "End Namespace"
                Return lTokens(0).Equals("End", StringComparison.OrdinalIgnoreCase) AndAlso
                       lTokens(1).Equals("Namespace", StringComparison.OrdinalIgnoreCase)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsEndNamespaceStatement error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Determines if a line is a local variable declaration (inside a method)
        ''' rather than a class-level field declaration
        ''' </summary>
        Private Function IsLocalVariableDeclaration(vLine As String) As Boolean
            Try
                ' Common patterns that indicate local variables:
                
                ' 1. Starts with "Dim" - always a local variable when inside a method
                If Regex.IsMatch(vLine, "^\s*Dim\s+", RegexOptions.IgnoreCase) Then
                    Return True
                End If
                
                ' 2. Variable assignment without field modifiers
                ' Local: lVariable = value  or  lVariable As Type = value
                ' Field would have: Private/Public/Protected/Friend
                If Regex.IsMatch(vLine, "^\s*[lpvg]\w+\s*(As\s+.+)?\s*=", RegexOptions.IgnoreCase) Then
                    ' Check it doesn't have field modifiers
                    If Not Regex.IsMatch(vLine, "^\s*(Private|Public|Protected|Friend|Shared)\s+", RegexOptions.IgnoreCase) Then
                        Return True
                    End If
                End If
                
                ' 3. For/For Each loop variables
                If Regex.IsMatch(vLine, "^\s*(for|for\s+each)\s+", RegexOptions.IgnoreCase) Then
                    Return True
                End If
                
                ' 4. Using statement variables
                If Regex.IsMatch(vLine, "^\s*Using\s+", RegexOptions.IgnoreCase) Then
                    Return True
                End If
                
                ' 5. Catch statement variables
                If Regex.IsMatch(vLine, "^\s*Catch\s+\w+\s+As\s+", RegexOptions.IgnoreCase) Then
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsLocalVariableDeclaration error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Replace: SimpleIDE.Managers.ProjectParser.ParseTypeDeclaration
        ''' <summary>
        ''' Parses type declaration and creates node (modified to return node and handle XML docs)
        ''' </summary>
        Private Function ParseTypeDeclaration(vLine As String, vParentNode As SyntaxNode, ByRef vTypeNode As SyntaxNode, vLines As List(Of String), vLineIndex As Integer) As Boolean
            Try
                ' Simple approach - use a more direct parsing method
                Dim lLine As String = vLine.Trim()
                
                ' Find the type keyword position
                Dim lTypeKeyword As String = ""
                Dim lTypeKeywordPos As Integer = -1
                Dim lTypeKeywords As String() = {"Class", "Module", "Interface", "Structure", "Struct", "Enum", "Delegate"}
                
                for each lKeyword in lTypeKeywords
                    ' Look for the keyword with word boundaries
                    Dim lPattern As String = "\b" & lKeyword & "\b"
                    Dim lMatch As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(lLine, lPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    If lMatch.Success Then
                        lTypeKeyword = lKeyword
                        lTypeKeywordPos = lMatch.Index
                        Exit for
                    End If
                Next
                
                If lTypeKeywordPos < 0 Then
                    Return False
                End If
                
                ' Extract the type name - it's the next word after the keyword
                Dim lAfterKeyword As String = lLine.Substring(lTypeKeywordPos + lTypeKeyword.Length).TrimStart()
                
                ' The type name is the next identifier
                Dim lTypeName As String = ""
                Dim lNameMatch As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(lAfterKeyword, "^([A-Za-z_][A-Za-z0-9_]*)")
                If lNameMatch.Success Then
                    lTypeName = lNameMatch.Groups(1).Value
                Else
                    Console.WriteLine($"  WARNING: Could not extract type name from: {lAfterKeyword}")
                    Return False
                End If
                
                ' Determine node type
                Dim lNodeType As CodeNodeType
                Select Case lTypeKeyword.ToUpper()
                    Case "CLASS"
                        lNodeType = CodeNodeType.eClass
                    Case "MODULE"
                        lNodeType = CodeNodeType.eModule
                    Case "INTERFACE"
                        lNodeType = CodeNodeType.eInterface
                    Case "STRUCTURE", "STRUCT"
                        lNodeType = CodeNodeType.eStructure
                    Case "ENUM"
                        lNodeType = CodeNodeType.eEnum
                    Case "DELEGATE"
                        lNodeType = CodeNodeType.eDelegate
                    Case Else
                        Return False
                End Select
                
                ' Check for partial types
                Dim lIsPartial As Boolean = System.Text.RegularExpressions.Regex.IsMatch(lLine.Substring(0, lTypeKeywordPos), "\bPartial\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                
                ' Handle partial types
                If lIsPartial Then
                    vTypeNode = HandlePartialType(vParentNode, lTypeName, lNodeType, pCurrentFile)
                Else
                    ' Create new type node
                    vTypeNode = New SyntaxNode(lNodeType, lTypeName)
                    vTypeNode.FilePath = pCurrentFile
                    vTypeNode.StartLine = vLineIndex
                    vParentNode.AddChild(vTypeNode)
                End If
                
                If vTypeNode Is Nothing Then
                    Console.WriteLine($"  ERROR: Failed to create type node for {lTypeName}")
                    Return False
                End If
                
                ' Parse modifiers (Public, Private, etc.)
                ParseTypeModifiers(vLine, vTypeNode)
                
                ' Parse inheritance and implementation
                ParseInheritanceAndImplementation(vLine, vTypeNode)
                
                ' Extract and apply XML documentation if available
                If vLines IsNot Nothing AndAlso vLineIndex > 0 Then
                    ExtractAndApplyXmlDocumentation(vLines, vLineIndex, vTypeNode)
                End If
                
                Console.WriteLine($"  Added {lTypeKeyword}: {lTypeName} To {vParentNode.Name}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseTypeDeclaration error: {ex.Message}")
                Console.WriteLine($"  Line: {vLine}")
                Return False
            End Try
        End Function
                
        ' Replace: SimpleIDE.Managers.ProjectParser.ParseMethodOrFunction (fix line 608)
        ''' <summary>
        ''' Parses method and function declarations (Sub/Function)
        ''' </summary>
        Private Function ParseMethodOrFunction(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer) As Boolean
            Try
                ' FIXED: Use List(Of String) instead of String()
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count < 2 Then Return False
                
                ' Find "Sub" or "Function" keyword
                Dim lMethodIndex As Integer = -1
                Dim lMethodType As String = ""
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Sub", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lTokens(i), "Function", StringComparison.OrdinalIgnoreCase) Then
                        lMethodIndex = i
                        lMethodType = lTokens(i)
                        Exit for
                    End If
                Next
                
                If lMethodIndex < 0 OrElse lMethodIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get method name (next token after "Sub" or "Function")
                Dim lName As String = lTokens(lMethodIndex + 1)
                
                ' Remove any parentheses from name
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                ' Determine node type
                Dim lNodeType As CodeNodeType
                If String.Equals(lName, "New", StringComparison.OrdinalIgnoreCase) Then
                    lNodeType = CodeNodeType.eConstructor
                ElseIf String.Equals(lMethodType, "Function", StringComparison.OrdinalIgnoreCase) Then
                    lNodeType = CodeNodeType.eFunction
                Else
                    lNodeType = CodeNodeType.eMethod
                End If
                
                ' Create member node
                Dim lMemberNode As New SyntaxNode(lNodeType, lName)
                lMemberNode.FilePath = pCurrentFile
                lMemberNode.StartLine = vLineNumber
                
                ' Parse modifiers (all tokens before "Sub" or "Function")
                for i As Integer = 0 To lMethodIndex - 1
                    If IsModifier(lTokens(i)) Then
                        ApplyModifierToNode(lTokens(i), lMemberNode)
                    End If
                Next
                
                ' Parse parameters (simplified - just store the whole parameter string)
                ParseParameters(vLine, lMemberNode)
                
                ' Parse return type for functions - look for "As" after closing parenthesis
                If lNodeType = CodeNodeType.eFunction Then
                    ParseReturnType(vLine, lMemberNode)
                End If
                
                ' Check for duplicate members in partial classes
                If Not IsDuplicateMember(vTypeNode, lMemberNode) Then
                    vTypeNode.AddChild(lMemberNode)
                    Console.WriteLine($"  Added {lMethodType}: {lName} To {vTypeNode.Name}")
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseMethodOrFunction error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses property declarations including single-line properties
        ''' </summary>
        Private Function ParseProperty(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer) As Boolean
            Try
                ' FIXED: Use List(Of String) instead of String()
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count < 2 Then Return False
                
                ' Find "Property" keyword
                Dim lPropertyIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Property", StringComparison.OrdinalIgnoreCase) Then
                        lPropertyIndex = i
                        Exit for
                    End If
                Next
                
                If lPropertyIndex < 0 OrElse lPropertyIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get property name (next token after "Property")
                Dim lName As String = lTokens(lPropertyIndex + 1)
                
                ' Remove any parentheses from name
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                ' Create property node
                Dim lPropertyNode As New SyntaxNode(CodeNodeType.eProperty, lName)
                lPropertyNode.FilePath = pCurrentFile
                lPropertyNode.StartLine = vLineNumber
                
                ' For single-line properties, end line is the same as start line
                lPropertyNode.EndLine = vLineNumber
                
                ' Parse modifiers (all tokens before "Property")
                for i As Integer = 0 To lPropertyIndex - 1
                    If IsModifier(lTokens(i)) Then
                        ApplyModifierToNode(lTokens(i), lPropertyNode)
                    End If
                Next
                
                ' Parse property type - look for "As" keyword
                Dim lAsIndex As Integer = -1
                for i As Integer = lPropertyIndex + 2 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "As", StringComparison.OrdinalIgnoreCase) Then
                        lAsIndex = i
                        Exit for
                    End If
                Next
                
                If lAsIndex >= 0 AndAlso lAsIndex < lTokens.Count - 1 Then
                    ' Get type - everything after "As" until equals sign or end
                    Dim lTypeTokens As New List(Of String)()
                    for i As Integer = lAsIndex + 1 To lTokens.Count - 1
                        If lTokens(i) = "=" Then
                            Exit for
                        End If
                        lTypeTokens.Add(lTokens(i))
                    Next
                    
                    If lTypeTokens.Count > 0 Then
                        lPropertyNode.ReturnType = String.Join(" ", lTypeTokens)
                    End If
                End If
                
                ' Check if this is a single-line property
                Dim lIsSingleLine As Boolean = True
                for each lToken in lTokens
                    If String.Equals(lToken, "Get", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lToken, "Set", StringComparison.OrdinalIgnoreCase) Then
                        lIsSingleLine = False
                        Exit for
                    End If
                Next
                
                ' FIXED: Store single-line flag in Attributes instead of non-existent IsSingleLine property
                If lIsSingleLine Then
                    ' Mark as single-line property using Attributes dictionary
                    If lPropertyNode.Attributes Is Nothing Then
                        lPropertyNode.Attributes = New Dictionary(Of String, String)()
                    End If
                    lPropertyNode.Attributes("IsSingleLine") = "True"
                    Console.WriteLine($"  Added Single-line Property: {lName} To {vTypeNode.Name}")
                Else
                    Console.WriteLine($"  Added Property: {lName} To {vTypeNode.Name}")
                End If
                
                ' Check for duplicate
                If Not IsDuplicateMember(vTypeNode, lPropertyNode) Then
                    vTypeNode.AddChild(lPropertyNode)
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseProperty error: {ex.Message}")
                Return False
            End Try
        End Function


    
        ''' <summary>
        ''' Parses enum member declarations
        ''' </summary>
        Private Function ParseEnum(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer) As Boolean
            Try
                ' Skip empty lines and comments
                Dim lTrimmed As String = vLine.Trim()
                If String.IsNullOrEmpty(lTrimmed) OrElse lTrimmed.StartsWith("'") Then
                    Return False
                End If
                
                ' Skip End Enum
                If Regex.IsMatch(lTrimmed, "^\s*End\s+Enum\s*", RegexOptions.IgnoreCase) Then
                    Return False
                End If
                
                ' FIXED: Use List(Of String) instead of String()
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count = 0 Then Return False
                
                ' First token should be the enum member name
                Dim lName As String = lTokens(0)
                
                ' Skip if it's a modifier keyword (shouldn't be in enum)
                If IsModifier(lName) Then
                    Return False
                End If
                
                ' Create enum member node
                Dim lEnumMemberNode As New SyntaxNode(CodeNodeType.eEnum, lName)
                lEnumMemberNode.FilePath = pCurrentFile
                lEnumMemberNode.StartLine = vLineNumber
                
                ' Check for value assignment
                Dim lEqualsIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If lTokens(i) = "=" Then
                        lEqualsIndex = i
                        Exit for
                    End If
                Next
                
                If lEqualsIndex >= 0 AndAlso lEqualsIndex < lTokens.Count - 1 Then
                    ' Get the value (everything after =)
                    Dim lValueTokens As New List(Of String)()
                    for i As Integer = lEqualsIndex + 1 To lTokens.Count - 1
                        lValueTokens.Add(lTokens(i))
                    Next
                    
                    If lValueTokens.Count > 0 Then
                        Dim lValue As String = String.Join(" ", lValueTokens)
                        ' Store value in Attributes
                        If lEnumMemberNode.Attributes Is Nothing Then
                            lEnumMemberNode.Attributes = New Dictionary(Of String, String)()
                        End If
                        lEnumMemberNode.Attributes("Value") = lValue
                    End If
                End If
                
                ' Add to enum type
                vTypeNode.AddChild(lEnumMemberNode)
                Console.WriteLine($"  Added Enum Member: {lName} To {vTypeNode.Name}")
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseEnum error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses field declarations with XML documentation support
        ''' </summary>
        Private Function ParseField(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer, vLines As List(Of String)) As Boolean
            Try
                ' Fields are typically Dim, Private, Public, Protected, Friend declarations
                ' that aren't methods, properties, events, or constants
                
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count < 2 Then Return False
                
                ' Check if this looks like a field declaration
                Dim lFieldKeywords As String() = {"Dim", "Private", "Public", "Protected", "Friend", "Shared", "ReadOnly", "WithEvents"}
                Dim lHasFieldKeyword As Boolean = False
                Dim lNameIndex As Integer = -1
                
                for i As Integer = 0 To lTokens.Count - 1
                    for each lKeyword in lFieldKeywords
                        If String.Equals(lTokens(i), lKeyword, StringComparison.OrdinalIgnoreCase) Then
                            lHasFieldKeyword = True
                            ' The name is typically the next non-keyword token
                            for j As Integer = i + 1 To lTokens.Count - 1
                                Dim lIsKeyword As Boolean = False
                                for each lKw in lFieldKeywords
                                    If String.Equals(lTokens(j), lKw, StringComparison.OrdinalIgnoreCase) Then
                                        lIsKeyword = True
                                        Exit for
                                    End If
                                Next
                                If Not lIsKeyword Then
                                    lNameIndex = j
                                    Exit for
                                End If
                            Next
                            Exit for
                        End If
                    Next
                    If lNameIndex >= 0 Then Exit for
                Next
                
                If Not lHasFieldKeyword OrElse lNameIndex < 0 OrElse lNameIndex >= lTokens.Count Then
                    Return False
                End If
                
                ' Make sure this isn't a method, property, etc.
                for each lToken in lTokens
                    If String.Equals(lToken, "Sub", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lToken, "Function", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lToken, "Property", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lToken, "Event", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lToken, "Const", StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(lToken, "Delegate", StringComparison.OrdinalIgnoreCase) Then
                        Return False
                    End If
                Next
                
                ' Get field name
                Dim lName As String = lTokens(lNameIndex)
                
                ' Remove any type declaration or assignment from name
                Dim lAsIndex As Integer = lName.IndexOf(" As ")
                If lAsIndex >= 0 Then
                    lName = lName.Substring(0, lAsIndex)
                End If
                
                Dim lEqualsIndex As Integer = lName.IndexOf("="c)
                If lEqualsIndex >= 0 Then
                    lName = lName.Substring(0, lEqualsIndex)
                End If
                
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                lName = lName.Trim()
                
                ' Skip if name is empty or looks invalid
                If String.IsNullOrEmpty(lName) OrElse Not IsValidIdentifier(lName) Then
                    Return False
                End If
                
                ' Create field node
                Dim lFieldNode As New SyntaxNode(CodeNodeType.eField, lName)
                lFieldNode.StartLine = vLineNumber
                lFieldNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, lFieldNode)
                
                ' Parse type if specified
                ParseFieldType(vLine, lFieldNode)
                
                ' Extract initial value if present
                Dim lValueMatch As Match = Regex.Match(vLine, "=\s*(.+?)(\s*'|$)")
                If lValueMatch.Success Then
                    lFieldNode.InitialValue = lValueMatch.Groups(1).Value.Trim()
                End If
                
                ' Extract and apply XML documentation
                ExtractAndApplyXmlDocumentation(vLines, vLineNumber, lFieldNode)
                
                ' Add to parent
                vTypeNode.AddChild(lFieldNode)
                
                Console.WriteLine($"  Added Field: {lName} To {vTypeNode.Name}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseField error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses event declarations with XML documentation support
        ''' </summary>
        Private Function ParseEvent(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer, vLines As List(Of String)) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                ' Find "Event" keyword
                Dim lEventIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Event", StringComparison.OrdinalIgnoreCase) Then
                        lEventIndex = i
                        Exit for
                    End If
                Next
                
                If lEventIndex < 0 OrElse lEventIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get event name
                Dim lName As String = lTokens(lEventIndex + 1)
                
                ' Remove any parentheses from name
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                ' Create event node
                Dim lEventNode As New SyntaxNode(CodeNodeType.eEvent, lName)
                lEventNode.StartLine = vLineNumber
                lEventNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, lEventNode)
                
                ' Parse parameters if present
                ParseParameters(vLine, lEventNode)
                
                ' Extract and apply XML documentation
                ExtractAndApplyXmlDocumentation(vLines, vLineNumber, lEventNode)
                
                ' Add to parent
                vTypeNode.AddChild(lEventNode)
                
                Console.WriteLine($"  Added Event: {lName} To {vTypeNode.Name}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseEvent error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Parses delegate declarations with XML documentation support
        ''' </summary>
        Private Function ParseDelegate(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer, vLines As List(Of String)) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                ' Find "Delegate" keyword
                Dim lDelegateIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Delegate", StringComparison.OrdinalIgnoreCase) Then
                        lDelegateIndex = i
                        Exit for
                    End If
                Next
                
                If lDelegateIndex < 0 Then Return False
                
                ' Determine if it's a Sub or Function delegate
                Dim lIsSub As Boolean = False
                Dim lIsFunction As Boolean = False
                Dim lNameIndex As Integer = -1
                
                for i As Integer = lDelegateIndex + 1 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Sub", StringComparison.OrdinalIgnoreCase) Then
                        lIsSub = True
                        lNameIndex = i + 1
                        Exit for
                    ElseIf String.Equals(lTokens(i), "Function", StringComparison.OrdinalIgnoreCase) Then
                        lIsFunction = True
                        lNameIndex = i + 1
                        Exit for
                    End If
                Next
                
                If lNameIndex < 0 OrElse lNameIndex >= lTokens.Count Then Return False
                
                ' Get delegate name
                Dim lName As String = lTokens(lNameIndex)
                
                ' Remove any parentheses from name
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex >= 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                ' Create delegate node
                Dim lDelegateNode As New SyntaxNode(CodeNodeType.eDelegate, lName)
                lDelegateNode.StartLine = vLineNumber
                lDelegateNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, lDelegateNode)
                
                ' Parse parameters
                ParseParameters(vLine, lDelegateNode)
                
                ' Parse return type (for function delegates)
                If lIsFunction Then
                    ParseReturnType(vLine, lDelegateNode)
                End If
                
                ' Extract and apply XML documentation
                ExtractAndApplyXmlDocumentation(vLines, vLineNumber, lDelegateNode)
                
                ' Add to parent
                vTypeNode.AddChild(lDelegateNode)
                
                Console.WriteLine($"  Added Delegate: {lName} To {vTypeNode.Name}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseDelegate error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses const declarations
        ''' </summary>
        Private Function ParseConst(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer) As Boolean
            Try
                Dim lPattern As String = "^\s*(?:(Public|Private|Protected|Friend)\s+)*Const\s+(\w+)"
                Dim lMatch As Match = Regex.Match(vLine, lPattern, RegexOptions.IgnoreCase)
                
                If Not lMatch.Success Then
                    Return False
                End If
                
                Dim lName As String = lMatch.Groups(2).Value
                
                ' Create const node
                Dim lConstNode As New SyntaxNode(CodeNodeType.eConst, lName)
                lConstNode.FilePath = pCurrentFile
                lConstNode.StartLine = vLineNumber
                lConstNode.IsConst = True
                
                ' Parse modifiers
                ParseModifiers(vLine, lConstNode)
                
                ' Check for duplicate
                If Not IsDuplicateMember(vTypeNode, lConstNode) Then
                    vTypeNode.AddChild(lConstNode)
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseConst error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses enum value declarations
        ''' </summary>
        Private Sub ParseEnumValue(vLine As String, vEnumNode As SyntaxNode, vLineNumber As Integer)
            Try
                ' Simple pattern for enum values
                Dim lPattern As String = "^\s*(\w+)\s*(=.*)?$"
                Dim lMatch As Match = Regex.Match(vLine, lPattern)
                
                If lMatch.Success AndAlso Not vLine.StartsWith("End ", StringComparison.OrdinalIgnoreCase) Then
                    Dim lName As String = lMatch.Groups(1).Value
                    
                    ' Create enum value node
                    Dim lEnumValueNode As New SyntaxNode(CodeNodeType.eEnumValue, lName)
                    lEnumValueNode.FilePath = pCurrentFile
                    lEnumValueNode.StartLine = vLineNumber
                    
                    vEnumNode.AddChild(lEnumValueNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseEnumValue error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods - Helpers =====
        
        ''' <summary>
        ''' Parses modifiers from a declaration line and updates the node
        ''' </summary>
        Private Sub ParseModifiers(vLine As String, vNode As SyntaxNode)
            Try
                vNode.IsPublic = Regex.IsMatch(vLine, "\bPublic\b", RegexOptions.IgnoreCase)
                vNode.IsPrivate = Regex.IsMatch(vLine, "\bPrivate\b", RegexOptions.IgnoreCase)
                vNode.IsProtected = Regex.IsMatch(vLine, "\bProtected\b", RegexOptions.IgnoreCase)
                vNode.IsFriend = Regex.IsMatch(vLine, "\bFriend\b", RegexOptions.IgnoreCase)
                vNode.IsShared = Regex.IsMatch(vLine, "\bShared\b", RegexOptions.IgnoreCase)
                vNode.IsOverridable = Regex.IsMatch(vLine, "\bOverridable\b", RegexOptions.IgnoreCase)
                vNode.IsOverrides = Regex.IsMatch(vLine, "\bOverrides\b", RegexOptions.IgnoreCase)
                vNode.IsMustOverride = Regex.IsMatch(vLine, "\bMustOverride\b", RegexOptions.IgnoreCase)
                vNode.IsNotOverridable = Regex.IsMatch(vLine, "\bNotOverridable\b", RegexOptions.IgnoreCase)
                vNode.IsMustInherit = Regex.IsMatch(vLine, "\bMustInherit\b", RegexOptions.IgnoreCase)
                vNode.IsNotInheritable = Regex.IsMatch(vLine, "\bNotInheritable\b", RegexOptions.IgnoreCase)
                vNode.IsReadOnly = Regex.IsMatch(vLine, "\bReadOnly\b", RegexOptions.IgnoreCase)
                vNode.IsWriteOnly = Regex.IsMatch(vLine, "\bWriteOnly\b", RegexOptions.IgnoreCase)
                vNode.IsWithEvents = Regex.IsMatch(vLine, "\bWithEvents\b", RegexOptions.IgnoreCase)
                vNode.IsShadows = Regex.IsMatch(vLine, "\bShadows\b", RegexOptions.IgnoreCase)
                vNode.IsAsync = Regex.IsMatch(vLine, "\bAsync\b", RegexOptions.IgnoreCase)
                
                ' Set visibility based on modifiers
                If vNode.IsPublic Then
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic
                ElseIf vNode.IsPrivate Then
                    vNode.Visibility = SyntaxNode.eVisibility.ePrivate
                ElseIf vNode.IsProtected AndAlso vNode.IsFriend Then
                    vNode.Visibility = SyntaxNode.eVisibility.eProtectedFriend
                ElseIf vNode.IsProtected Then
                    vNode.Visibility = SyntaxNode.eVisibility.eProtected
                ElseIf vNode.IsFriend Then
                    vNode.Visibility = SyntaxNode.eVisibility.eFriend
                Else
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic ' Default to public
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseModifiers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses type modifiers specifically for classes, modules, etc.
        ''' </summary>
        Private Sub ParseTypeModifiers(vLine As String, vNode As SyntaxNode)
            Try
                ParseModifiers(vLine, vNode)
                vNode.IsPartial = Regex.IsMatch(vLine, "\bPartial\b", RegexOptions.IgnoreCase)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseTypeModifiers error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses inheritance and implementation clauses
        ''' </summary>
        Private Sub ParseInheritanceAndImplementation(vLine As String, vNode As SyntaxNode)
            Try
                ' Parse Inherits clause
                Dim lInheritsMatch As Match = Regex.Match(vLine, "\bInherits\s+(\S+)", RegexOptions.IgnoreCase)
                If lInheritsMatch.Success Then
                    vNode.BaseType = lInheritsMatch.Groups(1).Value
                    vNode.InheritsList.Add(lInheritsMatch.Groups(1).Value)
                End If
                
                ' Parse Implements clause
                Dim lImplementsMatch As Match = Regex.Match(vLine, "\bImplements\s+(.+)$", RegexOptions.IgnoreCase)
                If lImplementsMatch.Success Then
                    Dim lInterfaces As String() = lImplementsMatch.Groups(1).Value.Split(","c)
                    for each lInterface in lInterfaces
                        vNode.ImplementsList.Add(lInterface.Trim())
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseInheritanceAndImplementation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses parameters from a method/function declaration
        ''' </summary>
        Private Sub ParseParameters(vLine As String, vNode As SyntaxNode)
            Try
                ' Extract parameters between parentheses
                Dim lParenStart As Integer = vLine.IndexOf("("c)
                Dim lParenEnd As Integer = vLine.LastIndexOf(")"c)
                
                If lParenStart >= 0 AndAlso lParenEnd > lParenStart Then
                    Dim lParamsString As String = vLine.Substring(lParenStart + 1, lParenEnd - lParenStart - 1)
                    
                    If Not String.IsNullOrWhiteSpace(lParamsString) Then
                        ' Split parameters by comma (simplified - doesn't handle nested generics)
                        Dim lParams As String() = lParamsString.Split(","c)
                        
                        for each lParam in lParams
                            Dim lTrimmedParam As String = lParam.Trim()
                            If Not String.IsNullOrEmpty(lTrimmedParam) Then
                                ' Parse parameter (simplified)
                                Dim lParamInfo As New ParameterInfo()
                                
                                ' Extract parameter name and type
                                Dim lParamMatch As Match = Regex.Match(lTrimmedParam, 
                                    "(?:(ByVal|ByRef|Optional|ParamArray)\s+)*(\w+)(?:\s+As\s+(.+))?", 
                                    RegexOptions.IgnoreCase)
                                
                                If lParamMatch.Success Then
                                    lParamInfo.Name = lParamMatch.Groups(2).Value
                                    If lParamMatch.Groups(3).Success Then
                                        ' FIXED: Use ParameterType instead of Type
                                        lParamInfo.ParameterType = lParamMatch.Groups(3).Value.Trim()
                                    End If
                                    lParamInfo.IsByRef = lTrimmedParam.IndexOf("ByRef", StringComparison.OrdinalIgnoreCase) >= 0
                                    lParamInfo.IsOptional = lTrimmedParam.IndexOf("Optional", StringComparison.OrdinalIgnoreCase) >= 0
                                    lParamInfo.IsParamArray = lTrimmedParam.IndexOf("ParamArray", StringComparison.OrdinalIgnoreCase) >= 0
                                    
                                    vNode.Parameters.Add(lParamInfo)
                                End If
                            End If
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseParameters error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses the return type from a function or property declaration
        ''' </summary>
        Private Sub ParseReturnType(vLine As String, vNode As SyntaxNode)
            Try
                ' Look for "As TypeName" pattern after closing parenthesis or property name
                Dim lTypeMatch As Match = Regex.Match(vLine, "\)\s+As\s+(.+)$|\bProperty\s+\w+(?:\([^)]*\))?\s+As\s+(.+)$", RegexOptions.IgnoreCase)
                
                If lTypeMatch.Success Then
                    If lTypeMatch.Groups(1).Success Then
                        vNode.ReturnType = lTypeMatch.Groups(1).Value.Trim()
                    ElseIf lTypeMatch.Groups(2).Success Then
                        vNode.ReturnType = lTypeMatch.Groups(2).Value.Trim()
                    End If
                    
                    ' Clean up return type (remove trailing comments, etc.)
                    Dim lCommentIndex As Integer = vNode.ReturnType.IndexOf("'"c)
                    If lCommentIndex >= 0 Then
                        vNode.ReturnType = vNode.ReturnType.Substring(0, lCommentIndex).Trim()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseReturnType error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Checks if a line is an End statement for a type using tokenization
        ''' </summary>
        Private Function IsEndTypeStatement(vLine As String) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                If lTokens.Count < 2 Then Return False
                
                ' Must start with "End"
                If Not lTokens(0).Equals("End", StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If
                
                ' Second token must be a type keyword
                Dim lTypeKeywords As String() = {"Class", "Module", "Interface", "Structure", "Enum"}
                for each lKeyword in lTypeKeywords
                    If lTokens(1).Equals(lKeyword, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsEndTypeStatement error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a member would be a duplicate in the type (for partial classes)
        ''' </summary>
        Private Function IsDuplicateMember(vTypeNode As SyntaxNode, vMemberNode As SyntaxNode) As Boolean
            Try
                ' For partial classes, check for exact duplicates
                If Not vTypeNode.IsPartial Then
                    Return False
                End If
                
                for each lExistingMember in vTypeNode.Children
                    ' Same name and type
                    If String.Equals(lExistingMember.Name, vMemberNode.Name, StringComparison.OrdinalIgnoreCase) AndAlso
                       lExistingMember.NodeType = vMemberNode.NodeType Then
                        
                        ' For methods/functions, check parameter count (simplified overload check)
                        If vMemberNode.NodeType = CodeNodeType.eMethod OrElse
                           vMemberNode.NodeType = CodeNodeType.eFunction OrElse
                           vMemberNode.NodeType = CodeNodeType.eConstructor Then
                            
                            ' If parameter counts differ, it's an overload, not a duplicate
                            If lExistingMember.Parameters.Count <> vMemberNode.Parameters.Count Then
                                Return False
                            End If
                        End If
                        
                        ' It's a duplicate
                        LogError($"Duplicate member '{vMemberNode.Name}' in partial class '{vTypeNode.Name}'")
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsDuplicateMember error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Validates the parsed structure
        ''' </summary>
        Private Sub ValidateStructure()
            Try
                ' Check for duplicate namespaces at same level
                ValidateNamespaces(pRootNode)
                
                ' Verify all partial classes are properly merged
                for each lKvp in pPartialClasses
                    Dim lNode As SyntaxNode = lKvp.Value
                    If lNode.Attributes.ContainsKey("FilePaths") Then
                        Dim lFileCount As Integer = lNode.Attributes("FilePaths").Split(";"c).Length
                        Console.WriteLine($"Partial Class '{lNode.Name}' merged from {lFileCount} files")
                    End If
                Next
                
                ' Update end lines for all nodes
                UpdateEndLines(pRootNode)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ValidateStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Validates namespace structure for duplicates
        ''' </summary>
        Private Sub ValidateNamespaces(vNode As SyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Check children for duplicate namespaces
                Dim lNamespaceNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                
                for each lChild in vNode.Children
                    If lChild.NodeType = CodeNodeType.eNamespace Then
                        If lNamespaceNames.Contains(lChild.Name) Then
                            LogError($"Duplicate Namespace '{lChild.Name}' at level")
                        Else
                            lNamespaceNames.Add(lChild.Name)
                        End If
                        
                        ' Recurse into child namespaces
                        ValidateNamespaces(lChild)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ValidateNamespaces error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates end lines for all nodes based on their children
        ''' </summary>
        Private Sub UpdateEndLines(vNode As SyntaxNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Process children first
                for each lChild in vNode.Children
                    UpdateEndLines(lChild)
                Next
                
                ' Update this node's end line based on children
                If vNode.Children.Count > 0 Then
                    Dim lMaxEndLine As Integer = vNode.StartLine
                    for each lChild in vNode.Children
                        If lChild.EndLine > lMaxEndLine Then
                            lMaxEndLine = lChild.EndLine
                        End If
                    Next
                    
                    ' Add a buffer for closing statements
                    vNode.EndLine = lMaxEndLine + 1
                Else
                    ' No children, end line is start line plus a small buffer
                    vNode.EndLine = vNode.StartLine + 1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.UpdateEndLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Counts total nodes in the tree
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
                Console.WriteLine($"ProjectParser.CountNodes error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Logs an error message
        ''' </summary>
        Private Sub LogError(vMessage As String)
            Dim lErrorMessage As String = $"[{pCurrentFile}:{pCurrentLineNumber}] {vMessage}"
            pParseErrors.Add(lErrorMessage)
            Console.WriteLine($"ProjectParser error: {lErrorMessage}")
        End Sub

'         ''' <summary>
'         ''' Enhanced version of ParseFileInNamespace that properly handles partial classes in explicit namespaces
'         ''' </summary>
'         ''' <param name="vFilePath">Path to the file being parsed</param>
'         ''' <param name="vFileContent">Content of the file</param>
'         ''' <returns>True if successfully parsed</returns>
'         Private Function ParseFileInNamespace(vFilePath As String, vFileContent As String) As Boolean
'             Try
'                 pCurrentFile = vFilePath
'                 Dim lLines As String() = vFileContent.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
'                 
'                 ' Track the current namespace context
'                 Dim lCurrentNamespace As SyntaxNode = pRootNamespace
'                 Dim lExplicitNamespaceStack As New Stack(Of SyntaxNode)()
'                 
'                 Dim i As Integer = 0
'                 While i < lLines.Length
'                     Dim lLine As String = lLines(i).Trim()
'                     pCurrentLineNumber = i
'                     
'                     ' Skip empty lines and comments
'                     If String.IsNullOrWhiteSpace(lLine) OrElse lLine.StartsWith("'") Then
'                         i += 1
'                         Continue While
'                     End If
'                     
'                     ' Check for explicit namespace declaration
'                     If lLine.StartsWith("Namespace ", StringComparison.OrdinalIgnoreCase) Then
'                         ' Extract namespace name
'                         Dim lNamespaceName As String = lLine.Substring(10).Trim()
'                         
'                         ' Special handling for Managers namespace
'                         If lNamespaceName = "Managers" Then
'                             ' Find or create Managers namespace under root
'                             Dim lManagersNS As SyntaxNode = Nothing
'                             for each lChild in pRootNamespace.Children
'                                 If lChild.NodeType = CodeNodeType.eNamespace AndAlso 
'                                    String.Equals(lChild.Name, "Managers", StringComparison.OrdinalIgnoreCase) Then
'                                     lManagersNS = lChild
'                                     Exit for
'                                 End If
'                             Next
'                             
'                             If lManagersNS Is Nothing Then
'                                 lManagersNS = New SyntaxNode(CodeNodeType.eNamespace, "Managers")
'                                 lManagersNS.FilePath = vFilePath
'                                 lManagersNS.StartLine = i
'                                 pRootNamespace.AddChild(lManagersNS)
'                                 Console.WriteLine($"Created Managers Namespace at line {i}")
'                             End If
'                             
'                             lCurrentNamespace = lManagersNS
'                             lExplicitNamespaceStack.Push(lManagersNS)
'                         Else
'                             ' Handle other namespaces normally
'                             Dim lNSNode As SyntaxNode = FindOrCreateNamespace(lNamespaceName, lCurrentNamespace)
'                             lNSNode.FilePath = vFilePath
'                             lNSNode.StartLine = i
'                             lCurrentNamespace = lNSNode
'                             lExplicitNamespaceStack.Push(lNSNode)
'                         End If
'                         
'                         Console.WriteLine($"Entered Namespace: {lNamespaceName} at line {i}")
'                         i += 1
'                         Continue While
'                     End If
'                     
'                     ' Check for End Namespace
'                     If lLine.StartsWith("End Namespace", StringComparison.OrdinalIgnoreCase) Then
'                         If lExplicitNamespaceStack.Count > 0 Then
'                             Dim lNS As SyntaxNode = lExplicitNamespaceStack.Pop()
'                             lNS.EndLine = i
'                             Console.WriteLine($"Exited Namespace: {lNS.Name} at line {i}")
'                             
'                             ' Reset to parent namespace or root
'                             If lExplicitNamespaceStack.Count > 0 Then
'                                 lCurrentNamespace = lExplicitNamespaceStack.Peek()
'                             Else
'                                 lCurrentNamespace = pRootNamespace
'                             End If
'                         End If
'                         i += 1
'                         Continue While
'                     End If
'                     
'                     ' Parse type declarations (Class, Module, etc.)
'                     Dim lTypeNode As SyntaxNode = Nothing
'                     If ParseTypeDeclaration(lLine, lCurrentNamespace, lTypeNode, lLines, i) Then
'                         If lTypeNode IsNot Nothing Then
'                             ' Special handling for ProjectInfo class
'                             If lTypeNode.Name = "ProjectInfo" AndAlso lCurrentNamespace.Name = "Managers" Then
'                                 Console.WriteLine($"Found ProjectInfo in Managers Namespace at line {i}")
'                                 ' It's already been added to Managers namespace by ParseTypeDeclaration
'                             End If
'                             
'                             ' Parse the type body
'                             i = ParseTypeBody(lLines, i + 1, lTypeNode)
'                         End If
'                     End If
'                     
'                     i += 1
'                 End While
'                 
'                 Return True
'                 
'             Catch ex As Exception
'                 Console.WriteLine($"ParseFileInNamespaceEnhanced error in {vFilePath}: {ex.Message}")
'                 pParseErrors.Add($"error parsing {vFilePath}: {ex.Message}")
'                 Return False
'             End Try
'         End Function

        ''' <summary>
        ''' Tokenizes a line of VB.NET code into individual tokens, filtering out invalid entries
        ''' </summary>
        ''' <param name="vLine">The line to tokenize</param>
        ''' <returns>List of valid tokens</returns>
        Private Function TokenizeLine(vLine As String) As List(Of String)
            Try
                Dim lTokens As New List(Of String)()
                If String.IsNullOrWhiteSpace(vLine) Then Return lTokens
                
                Dim lCurrentToken As New System.Text.StringBuilder()
                Dim lInString As Boolean = False
                Dim lInEscapedIdentifier As Boolean = False
                
                ' Define separators - but don't include them as standalone tokens unless they're operators
                Dim lSeparators As String = " ,(){}=<>+-*/" & vbTab
                Dim lOperatorChars As String = "(){}[]<>=+-*/:."
                
                for i As Integer = 0 To vLine.Length - 1
                    Dim lChar As Char = vLine(i)
                    
                    ' Handle escaped identifiers like [Enum] or [Class]
                    If Not lInString AndAlso lChar = "["c AndAlso Not lInEscapedIdentifier Then
                        ' Start of escaped identifier
                        lInEscapedIdentifier = True
                        ' Save current token
                        If lCurrentToken.Length > 0 Then
                            Dim lToken As String = lCurrentToken.ToString().Trim()
                            If IsValidToken(lToken) Then
                                lTokens.Add(lToken)
                            End If
                            lCurrentToken.Clear()
                        End If
                        ' Don't add [ as a token
                    ElseIf Not lInString AndAlso lChar = "]"c AndAlso lInEscapedIdentifier Then
                        ' End of escaped identifier
                        lInEscapedIdentifier = False
                        If lCurrentToken.Length > 0 Then
                            Dim lToken As String = lCurrentToken.ToString().Trim()
                            If IsValidToken(lToken) Then
                                lTokens.Add(lToken)
                            End If
                            lCurrentToken.Clear()
                        End If
                        ' Don't add ] as a token
                    ElseIf lChar = """"c AndAlso Not lInEscapedIdentifier Then
                        If i < vLine.Length - 1 AndAlso vLine(i + 1) = """"c Then
                            ' Escaped quote
                            If lInString Then
                                lCurrentToken.Append("""""")
                            End If
                            i += 1
                        Else
                            ' Toggle string state
                            lInString = Not lInString
                        End If
                    ElseIf lInString Then
                        ' Inside string literal
                        lCurrentToken.Append(lChar)
                    ElseIf lInEscapedIdentifier Then
                        ' Inside escaped identifier
                        lCurrentToken.Append(lChar)
                    ElseIf lChar = "'"c Then
                        ' Comment start - save current token and stop
                        If lCurrentToken.Length > 0 Then
                            Dim lToken As String = lCurrentToken.ToString().Trim()
                            If IsValidToken(lToken) Then
                                lTokens.Add(lToken)
                            End If
                        End If
                        Exit for
                    ElseIf lSeparators.Contains(lChar) Then
                        ' Separator found
                        If lCurrentToken.Length > 0 Then
                            Dim lToken As String = lCurrentToken.ToString().Trim()
                            If IsValidToken(lToken) Then
                                lTokens.Add(lToken)
                            End If
                            lCurrentToken.Clear()
                        End If
                        
                        ' DON'T add individual parentheses, brackets, etc. as tokens
                        ' unless they're part of a meaningful operator like ":=" or "<>"
                    Else
                        ' Regular character
                        lCurrentToken.Append(lChar)
                    End If
                Next
                
                ' Add final token
                If lCurrentToken.Length > 0 Then
                    Dim lToken As String = lCurrentToken.ToString().Trim()
                    If IsValidToken(lToken) Then
                        lTokens.Add(lToken)
                    End If
                End If
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.TokenizeLine error: {ex.Message}")
                Return New List(Of String)()
            End Try
        End Function
        
        ''' <summary>
        ''' Validates if a token is valid and should be included
        ''' </summary>
        ''' <param name="vToken">Token to validate</param>
        ''' <returns>True if valid, False otherwise</returns>
        Private Function IsValidToken(vToken As String) As Boolean
            Try
                ' Filter out invalid tokens
                If String.IsNullOrWhiteSpace(vToken) Then Return False
                
                ' Filter out standalone punctuation that shouldn't be tokens
                If vToken = ")" OrElse vToken = "(" OrElse vToken = "[" OrElse vToken = "]" OrElse
                   vToken = "{" OrElse vToken = "}" Then
                    Return False
                End If
                
                ' Filter out the word "all" if it appears in weird contexts
                ' (it should only appear as part of valid constructs like "for All")
                If vToken.ToLower() = "all" Then
                    ' This might be too aggressive - adjust if needed
                    Return False
                End If
                
                ' Valid token
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidToken error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Applies a modifier keyword to a syntax node
        ''' </summary>
        Private Sub ApplyModifierToNode(vModifier As String, vNode As SyntaxNode)
            Select Case vModifier.ToLower()
                Case "Public"
                    vNode.IsPublic = True
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic
                Case "Private"
                    vNode.IsPrivate = True
                    vNode.Visibility = SyntaxNode.eVisibility.ePrivate
                Case "Protected"
                    vNode.IsProtected = True
                    vNode.Visibility = SyntaxNode.eVisibility.eProtected
                Case "Friend"
                    vNode.IsFriend = True
                    vNode.Visibility = SyntaxNode.eVisibility.eFriend
                Case "Partial"
                    vNode.IsPartial = True
                Case "Shared"
                    vNode.IsShared = True
                Case "MustInherit"
                    vNode.IsMustInherit = True
                Case "NotInheritable"
                    vNode.IsNotInheritable = True
                Case "Shadows"
                    vNode.IsShadows = True
                Case "Overrides"
                    vNode.IsOverrides = True
                Case "Overridable"
                    vNode.IsOverridable = True
                Case "MustOverride"
                    vNode.IsMustOverride = True
                Case "NotOverridable"
                    vNode.IsNotOverridable = True
                Case "ReadOnly"
                    vNode.IsReadOnly = True
                Case "WriteOnly"
                    vNode.IsWriteOnly = True
                Case "WithEvents"
                    vNode.IsWithEvents = True
                Case "async"
                    vNode.IsAsync = True
            End Select
        End Sub

        ''' <summary>
        ''' Determines if a token is a VB.NET modifier keyword
        ''' </summary>
        ''' <param name="vToken">The token to check</param>
        ''' <returns>True if the token is a modifier keyword</returns>
        Private Function IsModifier(vToken As String) As Boolean
            Try
                If String.IsNullOrEmpty(vToken) Then Return False
                
                Dim lModifiers As String() = {
                    "Public", "Private", "Protected", "Friend",
                    "Shared", "Static", "Partial", "MustInherit", 
                    "NotInheritable", "Overridable", "NotOverridable",
                    "MustOverride", "Overrides", "Shadows", 
                    "ReadOnly", "WriteOnly", "Const", "WithEvents",
                    "Default", "Async", "Iterator"
                }
                
                for each lModifier in lModifiers
                    If String.Equals(vToken, lModifier, StringComparison.OrdinalIgnoreCase) Then
                        Return True
                    End If
                Next
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.IsModifier error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Parses inheritance and implementation from tokenized line
        ''' </summary>
        Private Sub ParseInheritanceFromTokens(vTokens As List(Of String), vStartIndex As Integer, vNode As SyntaxNode)
            Try
                If vStartIndex >= vTokens.Count Then Return
                
                Dim lInInherits As Boolean = False
                Dim lInImplements As Boolean = False
                Dim lCurrentName As New System.Text.StringBuilder()
                
                for i As Integer = vStartIndex To vTokens.Count - 1
                    Dim lToken As String = vTokens(i)
                    
                    If lToken.Equals("Inherits", StringComparison.OrdinalIgnoreCase) Then
                        lInInherits = True
                        lInImplements = False
                        If lCurrentName.Length > 0 Then
                            lCurrentName.Clear()
                        End If
                    ElseIf lToken.Equals("Implements", StringComparison.OrdinalIgnoreCase) Then
                        lInImplements = True
                        lInInherits = False
                        If lCurrentName.Length > 0 Then
                            lCurrentName.Clear()
                        End If
                    ElseIf lToken = "," Then
                        ' Comma separates multiple interfaces
                        If lCurrentName.Length > 0 Then
                            If lInImplements Then
                                vNode.ImplementsList.Add(lCurrentName.ToString())
                            End If
                            lCurrentName.Clear()
                        End If
                    ElseIf lToken.Length = 1 AndAlso "(){}[]<>=+-/*:".Contains(lToken) Then
                        ' Skip operators
                        Continue for
                    Else
                        ' Build up the name
                        If lCurrentName.Length > 0 AndAlso Not lToken.StartsWith(".") Then
                            lCurrentName.Append(".")
                        End If
                        lCurrentName.Append(lToken)
                        
                        If lInInherits AndAlso String.IsNullOrEmpty(vNode.BaseType) Then
                            vNode.BaseType = lCurrentName.ToString()
                            vNode.InheritsList.Add(lCurrentName.ToString())
                        End If
                    End If
                Next
                
                ' Add final implementation if any
                If lInImplements AndAlso lCurrentName.Length > 0 Then
                    vNode.ImplementsList.Add(lCurrentName.ToString())
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseInheritanceFromTokens error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Parses the type information for a field or constant
        ''' </summary>
        Private Sub ParseFieldType(vLine As String, vNode As SyntaxNode)
            Try
                ' Look for "As TypeName" pattern
                Dim lAsMatch As Match = Regex.Match(vLine, "\bAs\s+([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\([^)]*\))?)", RegexOptions.IgnoreCase)
                
                If lAsMatch.Success Then
                    vNode.ReturnType = lAsMatch.Groups(1).Value.Trim()
                    
                    ' Clean up the type (remove trailing comments, etc.)
                    Dim lCommentIndex As Integer = vNode.ReturnType.IndexOf("'"c)
                    If lCommentIndex >= 0 Then
                        vNode.ReturnType = vNode.ReturnType.Substring(0, lCommentIndex).Trim()
                    End If
                    
                    Dim lEqualsIndex As Integer = vNode.ReturnType.IndexOf("="c)
                    If lEqualsIndex >= 0 Then
                        vNode.ReturnType = vNode.ReturnType.Substring(0, lEqualsIndex).Trim()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseFieldType error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Parses constant declarations with XML documentation support
        ''' </summary>
        Private Function ParseConstant(vLine As String, vTypeNode As SyntaxNode, vLineNumber As Integer, vLines As List(Of String)) As Boolean
            Try
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                
                ' Find "Const" keyword
                Dim lConstIndex As Integer = -1
                for i As Integer = 0 To lTokens.Count - 1
                    If String.Equals(lTokens(i), "Const", StringComparison.OrdinalIgnoreCase) Then
                        lConstIndex = i
                        Exit for
                    End If
                Next
                
                If lConstIndex < 0 OrElse lConstIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get constant name
                Dim lName As String = lTokens(lConstIndex + 1)
                
                ' Remove any type declaration or assignment from name
                Dim lAsIndex As Integer = lName.IndexOf(" As ")
                If lAsIndex >= 0 Then
                    lName = lName.Substring(0, lAsIndex)
                End If
                
                Dim lEqualsIndex As Integer = lName.IndexOf("="c)
                If lEqualsIndex >= 0 Then
                    lName = lName.Substring(0, lEqualsIndex)
                End If
                
                lName = lName.Trim()
                
                ' Create constant node
                Dim lConstNode As New SyntaxNode(CodeNodeType.eConst, lName)
                lConstNode.StartLine = vLineNumber
                lConstNode.FilePath = pCurrentFile
                
                ' Parse modifiers
                ParseModifiers(vLine, lConstNode)
                
                ' Parse type if specified
                ParseFieldType(vLine, lConstNode)
                
                ' Extract initial value if present
                Dim lValueMatch As Match = Regex.Match(vLine, "=\s*(.+?)(\s*'|$)")
                If lValueMatch.Success Then
                    lConstNode.InitialValue = lValueMatch.Groups(1).Value.Trim()
                End If
                
                ' Extract and apply XML documentation
                ExtractAndApplyXmlDocumentation(vLines, vLineNumber, lConstNode)
                
                ' Add to parent
                vTypeNode.AddChild(lConstNode)
                
                Console.WriteLine($"  Added Const: {lName} To {vTypeNode.Name}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseConstant error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Parses the body of a type declaration (Class, Module, Interface, etc.)
        ''' </summary>
        ''' <param name="vLines">All lines in the file</param>
        ''' <param name="vStartLine">Starting line index of the type body</param>
        ''' <param name="vTypeNode">The type node to populate with members</param>
        ''' <returns>The line index after the type body ends</returns>
        Private Function ParseTypeBody(vLines As List(Of String), vStartLine As Integer, vTypeNode As SyntaxNode) As Integer
            Try
                Dim i As Integer = vStartLine
                Dim lNestLevel As Integer = 1
                
                While i < vLines.Count AndAlso lNestLevel > 0
                    Dim lLine As String = vLines(i).Trim()
                    
                    ' Skip empty lines and comments
                    If String.IsNullOrWhiteSpace(lLine) OrElse lLine.StartsWith("'") Then
                        i += 1
                        Continue While
                    End If
                    
                    ' Check for End statement
                    If IsEndTypeStatement(lLine) Then
                        lNestLevel -= 1
                        If lNestLevel = 0 Then
                            vTypeNode.EndLine = i
                            Return i
                        End If
                    End If
                    
                    ' Parse members within the type
                    If lNestLevel = 1 Then
                        ' Try to parse as method/function
                        If ParseMethodOrFunction(lLine, vTypeNode, i) Then
                            ' Skip to end of method
                            i = SkipToEndOfMethod(vLines, i + 1)
                        ' Try to parse as property
                        ElseIf ParseProperty(lLine, vTypeNode, i) Then
                            ' Skip to end of property if multi-line
                            If Not lLine.Contains("=") AndAlso Not lLine.Contains("Get") AndAlso Not lLine.Contains("Set") Then
                                i = SkipToEndOfProperty(vLines, i + 1)
                            End If
                        ' Try to parse as field
                        ElseIf ParseField(lLine, vTypeNode, i, vLines) Then
                            ' Single line - nothing more to do
                        ' Try to parse as event
                        ElseIf ParseEvent(lLine, vTypeNode, i, vLines) Then
                            ' Single line - nothing more to do
                        End If
                    End If
                    
                    i += 1
                End While
                
                Return i
                
            Catch ex As Exception
                Console.WriteLine($"ParseTypeBody error: {ex.Message}")
                Return vStartLine + 1
            End Try
        End Function
        
        ''' <summary>
        ''' Skips lines until the end of a method is found
        ''' </summary>
        Private Function SkipToEndOfMethod(vLines As List(Of String), vStartLine As Integer) As Integer
            Try
                Dim i As Integer = vStartLine
                Dim lNestLevel As Integer = 1
                
                While i < vLines.Count AndAlso lNestLevel > 0
                    Dim lLine As String = vLines(i).Trim()
                    
                    ' Check for nested blocks
                    If Regex.IsMatch(lLine, "\b(If|for|While|Do|Select|Try|Using|SyncLock|with)\b", RegexOptions.IgnoreCase) Then
                        ' Check if it's not a single-line If
                        If Not Regex.IsMatch(lLine, "\bThen\b.+\bEnd If\b", RegexOptions.IgnoreCase) Then
                            lNestLevel += 1
                        End If
                    ElseIf Regex.IsMatch(lLine, "^\s*End\s+(If|for|While|Do|Select|Try|Using|SyncLock|with)\b", RegexOptions.IgnoreCase) Then
                        lNestLevel -= 1
                    ElseIf Regex.IsMatch(lLine, "^\s*End\s+(Sub|Function)\b", RegexOptions.IgnoreCase) Then
                        lNestLevel -= 1
                        If lNestLevel = 0 Then
                            Return i
                        End If
                    End If
                    
                    i += 1
                End While
                
                Return i
                
            Catch ex As Exception
                Console.WriteLine($"SkipToEndOfMethod error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        ''' <summary>
        ''' Skips lines until the end of a property is found
        ''' </summary>
        Private Function SkipToEndOfProperty(vLines As List(Of String), vStartLine As Integer) As Integer
            Try
                Dim i As Integer = vStartLine
                
                While i < vLines.Count
                    Dim lLine As String = vLines(i).Trim()
                    
                    If Regex.IsMatch(lLine, "^\s*End\s+Property\b", RegexOptions.IgnoreCase) Then
                        Return i
                    End If
                    
                    i += 1
                End While
                
                Return i
                
            Catch ex As Exception
                Console.WriteLine($"SkipToEndOfProperty error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        ''' <summary>
        ''' Merges members from a source partial class into an existing partial class
        ''' </summary>
        ''' <param name="vExistingClass">The existing partial class to merge into</param>
        ''' <param name="vNewClass">The new partial class with members to merge</param>
        Private Sub MergePartialClass(vExistingClass As SyntaxNode, vNewClass As SyntaxNode)
            Try
                If vExistingClass Is Nothing OrElse vNewClass Is Nothing Then Return
                
                Console.WriteLine($"Merging Partial Class {vNewClass.Name} into existing Class")
                
                ' Mark both as partial
                vExistingClass.IsPartial = True
                vNewClass.IsPartial = True
                
                ' Merge file paths if tracked
                If vExistingClass.Attributes Is Nothing Then
                    vExistingClass.Attributes = New Dictionary(Of String, String)()
                End If
                
                If vExistingClass.Attributes.ContainsKey("FilePaths") Then
                    Dim lExistingPaths As String = vExistingClass.Attributes("FilePaths")
                    If Not lExistingPaths.Contains(vNewClass.FilePath) Then
                        vExistingClass.Attributes("FilePaths") = lExistingPaths & ";" & vNewClass.FilePath
                    End If
                Else
                    vExistingClass.Attributes("FilePaths") = vExistingClass.FilePath & ";" & vNewClass.FilePath
                End If
                
                ' Merge children (members) - avoid duplicates
                for each lNewMember in vNewClass.Children
                    If Not IsDuplicateMember(vExistingClass, lNewMember) Then
                        vExistingClass.AddChild(lNewMember)
                        Console.WriteLine($"  Merged member: {lNewMember.Name} ({lNewMember.NodeType})")
                    Else
                        Console.WriteLine($"  Skipped duplicate member: {lNewMember.Name}")
                    End If
                Next
                
                ' Update line ranges if needed
                If vNewClass.StartLine < vExistingClass.StartLine Then
                    vExistingClass.StartLine = vNewClass.StartLine
                End If
                If vNewClass.EndLine > vExistingClass.EndLine Then
                    vExistingClass.EndLine = vNewClass.EndLine
                End If
                
                ' Merge base types and interfaces if different
                If Not String.IsNullOrEmpty(vNewClass.BaseType) AndAlso String.IsNullOrEmpty(vExistingClass.BaseType) Then
                    vExistingClass.BaseType = vNewClass.BaseType
                End If
                
                for each lInterface in vNewClass.ImplementsList
                    If Not vExistingClass.ImplementsList.Contains(lInterface) Then
                        vExistingClass.ImplementsList.Add(lInterface)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"MergePartialClass error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Checks if a string is a valid VB.NET identifier
        ''' </summary>
        ''' <param name="vName">The name to check</param>
        ''' <returns>True if valid identifier, False otherwise</returns>
        Private Function IsValidIdentifier(vName As String) As Boolean
            Try
                If String.IsNullOrWhiteSpace(vName) Then Return False
                
                ' Filter out single punctuation characters
                If vName.Length = 1 Then
                    Dim lChar As Char = vName(0)
                    If lChar = ")"c OrElse lChar = "("c OrElse lChar = "["c OrElse lChar = "]"c OrElse
                       lChar = "{"c OrElse lChar = "}"c OrElse lChar = "."c OrElse lChar = ","c OrElse
                       lChar = ";"c OrElse lChar = ":"c OrElse lChar = "!"c OrElse lChar = "?"c Then
                        Return False
                    End If
                End If
                
                ' Filter out the word "all" as a standalone identifier (it's a contextual keyword)
                If String.Equals(vName, "all", StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If
                
                ' Must start with letter or underscore
                If Not (Char.IsLetter(vName(0)) OrElse vName(0) = "_"c) Then
                    Return False
                End If
                
                ' Rest must be letters, digits, or underscores
                for i As Integer = 1 To vName.Length - 1
                    If Not (Char.IsLetterOrDigit(vName(i)) OrElse vName(i) = "_"c) Then
                        Return False
                    End If
                Next
                
                ' Check it's not just underscores
                If vName.All(Function(c) c = "_"c) Then
                    Return False
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidIdentifier error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Information about string delimiters in a line
        ''' </summary>
        Private Structure StringDelimiterInfo
            Public HasOddQuotes As Boolean
            Public IsInterpolated As Boolean
            Public QuoteCount As Integer
        End Structure
        
        ''' <summary>
        ''' Checks if a line contains any string delimiter (regular or interpolated)
        ''' </summary>
        Private Function ContainsStringDelimiter(vLine As String) As Boolean
            ' Check for regular quotes
            If vLine.Contains("""") Then
                Return True
            End If
            
            ' Check for interpolated string start ($")
            If vLine.Contains("$""") Then
                Return True
            End If
            
            Return False
        End Function
        
        ''' <summary>
        ''' Analyzes string delimiters in a line, handling both regular and interpolated strings
        ''' </summary>
        Private Function AnalyzeStringDelimiters(vLine As String) As StringDelimiterInfo
            Try
                Dim lInfo As New StringDelimiterInfo()
                Dim lQuoteCount As Integer = 0
                Dim i As Integer = 0
                
                While i < vLine.Length
                    ' Check for interpolated string start ($")
                    If i < vLine.Length - 1 AndAlso vLine(i) = "$"c AndAlso vLine(i + 1) = """"c Then
                        lInfo.IsInterpolated = True
                        ' Count this as a quote
                        lQuoteCount += 1
                        i += 2 ' Skip both $ and "
                        Continue While
                    End If
                    
                    ' Check for regular quote
                    If vLine(i) = """"c Then
                        ' Check if it's an escaped quote ("")
                        If i < vLine.Length - 1 AndAlso vLine(i + 1) = """"c Then
                            ' Escaped quote - skip both, don't count
                            i += 2
                        Else
                            ' Real quote
                            lQuoteCount += 1
                            i += 1
                        End If
                    Else
                        i += 1
                    End If
                End While
                
                lInfo.QuoteCount = lQuoteCount
                lInfo.HasOddQuotes = (lQuoteCount Mod 2 = 1)
                
                Return lInfo
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.AnalyzeStringDelimiters error: {ex.Message}")
                Return New StringDelimiterInfo() with {.HasOddQuotes = False}
            End Try
        End Function

        ''' <summary>
        ''' Counts the number of quote marks in a line, properly handling escaped quotes and interpolated strings
        ''' </summary>
        ''' <param name="vLine">The line to count quotes in</param>
        ''' <returns>The number of unescaped quote marks</returns>
        Private Function CountQuotes(vLine As String) As Integer
            Try
                Dim lInfo As StringDelimiterInfo = AnalyzeStringDelimiters(vLine)
                Return lInfo.QuoteCount
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.CountQuotes error: {ex.Message}")
                Return 0
            End Try
        End Function

        ''' <summary>
        ''' Special handler for partial classes that need to be placed in specific namespaces
        ''' </summary>
        ''' <param name="vClassNode">The class node to process</param>
        ''' <param name="vDeclaredNamespace">The namespace declared in the file</param>
        ''' <param name="vRootNamespace">The project root namespace</param>
        Private Sub ProcessPartialClassInNamespace(vClassNode As SyntaxNode, vDeclaredNamespace As String, vRootNamespace As SyntaxNode)
            Try
                ' Special case: ProjectInfo is declared in Managers namespace but inherits from ProjectFileParser.ProjectInfo
                If vClassNode.Name = "ProjectInfo" AndAlso vDeclaredNamespace = "Managers" Then
                    ' Find or create the Managers namespace
                    Dim lManagersNamespace As SyntaxNode = Nothing
                    
                    ' Search for existing Managers namespace in root
                    for each lChild in vRootNamespace.Children
                        If lChild.NodeType = CodeNodeType.eNamespace AndAlso 
                           String.Equals(lChild.Name, "Managers", StringComparison.OrdinalIgnoreCase) Then
                            lManagersNamespace = lChild
                            Exit for
                        End If
                    Next
                    
                    ' Create Managers namespace if not found
                    If lManagersNamespace Is Nothing Then
                        lManagersNamespace = New SyntaxNode(CodeNodeType.eNamespace, "Managers")
                        lManagersNamespace.IsImplicit = False
                        vRootNamespace.AddChild(lManagersNamespace)
                        Console.WriteLine("Created Managers Namespace under root")
                    End If
                    
                    ' Move or merge the class into Managers namespace
                    Dim lExistingClass As SyntaxNode = Nothing
                    for each lChild in lManagersNamespace.Children
                        If lChild.NodeType = CodeNodeType.eClass AndAlso
                           String.Equals(lChild.Name, vClassNode.Name, StringComparison.OrdinalIgnoreCase) Then
                            lExistingClass = lChild
                            Exit for
                        End If
                    Next
                    
                    If lExistingClass IsNot Nothing Then
                        ' Merge with existing partial class
                        MergePartialClass(lExistingClass, vClassNode)
                        Console.WriteLine($"Merged ProjectInfo into existing Class in Managers Namespace")
                    Else
                        ' Add new class to Managers namespace
                        lManagersNamespace.AddChild(vClassNode)
                        Console.WriteLine($"Added ProjectInfo To Managers Namespace")
                    End If
                    
                    ' Remove from root if it was incorrectly placed there
                    If vRootNamespace.Children.Contains(vClassNode) Then
                        vRootNamespace.Children.Remove(vClassNode)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProcessPartialClassInNamespace error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace