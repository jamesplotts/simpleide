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
    Partial Public Class ProjectParser
        
        ' ===== Private Fields =====
        Private pProjectManager As ProjectManager
        Private pRootNode As SyntaxNode
        Private pRootNamespace As SyntaxNode
        Private pPartialClasses As Dictionary(Of String, SyntaxNode)
        Private pParseErrors As List(Of String)
        Private pCurrentFile As String
        Private pCurrentLineNumber As Integer
        
        ''' <summary>
        ''' The unified project syntax tree
        ''' </summary>
        Private pProjectSyntaxTree As SyntaxNode

        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when parsing of the project structure is completed
        ''' </summary>
        ''' <param name="vRootNode">The root node of the parsed syntax tree</param>
        Public Event ParseCompleted(vRootNode As SyntaxNode)
        
        ''' <summary>
        ''' Raised when the project structure has been loaded
        ''' </summary>
        ''' <param name="vRootNode">The root node of the project structure</param>
        Public Event ProjectStructureLoaded(vRootNode As SyntaxNode)

        ''' <summary>
        ''' Gets or sets the line metadata array for syntax highlighting and structure
        ''' </summary>
        ''' <value>Array of LineMetadata objects, one per line</value>
        Public Property LineMetadata As LineMetadata()


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

                    ' Process pending GTK events to keep UI responsive
                    While Gtk.Application.EventsPending()
                        Gtk.Application.RunIteration(False)
                    End While
                    If lSourceFile.IsLoaded AndAlso lSourceFile.TextLines IsNot Nothing AndAlso lSourceFile.TextLines.Count > 0 Then
                        lFilesWithContent += 1
                        Console.WriteLine($"   {System.IO.Path.GetFileName(lFileEntry.Key)}: {lSourceFile.TextLines.Count} lines")
                    Else
                        lFilesWithoutContent += 1
                        Console.WriteLine($"   {System.IO.Path.GetFileName(lFileEntry.Key)}: Not loaded or no lines")
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
                'Console.WriteLine($"MergePartialClasses: Processing {pPartialClasses.Count} Partial Class entries")
                
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
                        'Console.WriteLine($"  Partial Class '{lMainClass.Name}' merged from {lFileCount} files")
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
        ''' Parses content for syntax highlighting without full project context
        ''' </summary>
        ''' <param name="vContent">The source code content to parse</param>
        ''' <param name="vFilePath">Path to the file being parsed</param>
        ''' <param name="vRootNamespace">Root namespace for the parse context</param>
        ''' <returns>ParseResult containing syntax tree and line metadata</returns>
        ''' <remarks>
        ''' This method is used for single-file parsing, typically for syntax highlighting
        ''' in editors that aren't part of a project. Creates temporary parsing structures.
        ''' </remarks>
        Public Function ParseContent(vContent As String, vFilePath As String, vRootNamespace As String) As ParseResult
            Try
                'Console.WriteLine($"ProjectParser.ParseContent: Parsing content for {vFilePath}")
                
                ' Create a temporary SourceFileInfo for parsing
                ' FIXED: Added the third parameter for project directory
                Dim lTempFile As New SourceFileInfo(vFilePath, "", System.IO.Path.GetDirectoryName(vFilePath))
                lTempFile.Content = vContent
                lTempFile.IsLoaded = True
                
                ' CRITICAL FIX: Split content into TextLines for GenerateLineMetadata to work
                lTempFile.TextLines = New List(Of String)(vContent.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None))
                'Console.WriteLine($"ProjectParser.ParseContent: Split content into {lTempFile.TextLines.Count} lines")
                
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
                Dim lResult As New Syntax.ParseResult()
                lResult.RootNode = lTempRoot
                
                ' CRITICAL FIX: Generate LineMetadata with syntax tokens for each line
                GenerateLineMetadata(lTempFile, lResult)
                
                ' Restore original structures
                pRootNode = lOriginalRoot
                pRootNamespace = lOriginalNamespace
                
                ' Return result with LineMetadata
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseContent error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                Return New ParseResult()
            End Try
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



        Private Sub ParseFileContent(vFilePath As String, vSourceFileInfo As SourceFileInfo)
            ParseFileContent(vFilePath, vSourceFileInfo.TextLines)
        End Sub

        ''' <summary>
        ''' Parses the content of a single file and adds to the project tree
        ''' </summary>
        ''' <param name="vFilePath">Path to the file being parsed</param>
        ''' <param name="vLines">The lines of the file</param>
        ''' <remarks>
        ''' Enhanced with detailed logging to debug partial class merging issues
        ''' </remarks>
        Private Sub ParseFileContent(vFilePath As String, vLines As List(Of String))
            Try
                If vLines Is Nothing OrElse vLines.Count = 0 Then Return
                
                Console.WriteLine($"ParseFileContent: Starting parse of {System.IO.Path.GetFileName(vFilePath)} ({vLines.Count} lines)")
                pCurrentFile = vFilePath
                
                ' Track current context
                Dim lCurrentNamespace As SyntaxNode = pRootNamespace
                Dim lCurrentType As SyntaxNode = Nothing
                Dim lTypeStack As New Stack(Of SyntaxNode)()
                Dim lInMethodBody As Boolean = False
                Dim lTypesFound As Integer = 0
                Dim lMethodsFound As Integer = 0
                Dim lPropertiesFound As Integer = 0
                
                ' Parse line by line
                Dim i As Integer = 0
                While i < vLines.Count
                    Dim lLine As String = vLines(i)
                    Dim lTrimmedLine As String = lLine.Trim()
                    
                    ' Skip empty lines and comments at file level
                    If String.IsNullOrWhiteSpace(lTrimmedLine) OrElse lTrimmedLine.StartsWith("'") Then
                        i += 1
                        Continue While
                    End If
                    
                    ' Handle XML documentation (skip to next non-doc line)
                    If lTrimmedLine.StartsWith("'''") Then
                        While i < vLines.Count AndAlso vLines(i).Trim().StartsWith("'''")
                            i += 1
                        End While
                        Continue While
                    End If
                    
                    ' Check for namespace declaration
                    If lTrimmedLine.StartsWith("Namespace ", StringComparison.OrdinalIgnoreCase) Then
                        Dim lNamespaceName As String = ExtractNamespaceName(lTrimmedLine)
                        If Not String.IsNullOrEmpty(lNamespaceName) Then
                            Console.WriteLine($"  Found namespace: {lNamespaceName}")
                            lCurrentNamespace = GetOrCreateNamespace(lNamespaceName)
                        End If
                        i += 1
                        Continue While
                    End If
                    
                    ' Check for End Namespace
                    If lTrimmedLine.Equals("End Namespace", StringComparison.OrdinalIgnoreCase) Then
                        lCurrentNamespace = pRootNamespace
                        i += 1
                        Continue While
                    End If
                    
                    ' Check for End statements while in method body
                    If lInMethodBody AndAlso IsEndMethodStatement(lTrimmedLine) Then
                        lInMethodBody = False
                        i += 1
                        Continue While
                    End If
                    
                    ' Skip lines inside method bodies
                    If lInMethodBody Then
                        i += 1
                        Continue While
                    End If
                    
                    ' Try to parse as type declaration
                    Dim lTypeNode As SyntaxNode = Nothing
                    
                    If ParseTypeDeclaration(lTrimmedLine, lCurrentNamespace, lTypeNode, vLines, i) Then
                        If lTypeNode IsNot Nothing Then
                            lTypesFound += 1
                            Console.WriteLine($"  Found type: {lTypeNode.Name} ({lTypeNode.NodeType}), IsPartial={lTypeNode.IsPartial}")
                            lCurrentType = lTypeNode
                            lTypeStack.Push(lCurrentType)
                            
                            ' Parse the type body and count members
                            Dim lStartMembers As Integer = If(lCurrentType?.Children?.Count, 0)
                            i = ParseTypeBody(vLines, i + 1, lTypeNode)
                            Dim lEndMembers As Integer = If(lCurrentType?.Children?.Count, 0)
                            Dim lMembersAdded As Integer = lEndMembers - lStartMembers
                            
                            Console.WriteLine($"    Type body parsed: Added {lMembersAdded} members")
                            
                            ' Log member summary
                            If lCurrentType IsNot Nothing AndAlso lCurrentType.Children.Count > 0 Then
                                Dim lMethodCount As Integer = 0
                                Dim lPropertyCount As Integer = 0
                                Dim lFieldCount As Integer = 0
                                Dim lEventCount As Integer = 0
                                
                                For Each lMember In lCurrentType.Children
                                    Select Case lMember.NodeType
                                        Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor
                                            lMethodCount += 1
                                        Case CodeNodeType.eProperty
                                            lPropertyCount += 1
                                        Case CodeNodeType.eField, CodeNodeType.eConst
                                            lFieldCount += 1
                                        Case CodeNodeType.eEvent
                                            lEventCount += 1
                                    End Select
                                Next
                                
                                Console.WriteLine($"    Members: {lMethodCount} methods, {lPropertyCount} properties, {lFieldCount} fields, {lEventCount} events")
                            End If
                            
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
                                lMethodsFound += 1
                                ' Extract method name for logging
                                Dim lTokens As List(Of String) = TokenizeLine(lTrimmedLine)
                                Dim lMethodName As String = ""
                                For j As Integer = 0 To lTokens.Count - 2
                                    If lTokens(j).Equals("Sub", StringComparison.OrdinalIgnoreCase) OrElse
                                       lTokens(j).Equals("Function", StringComparison.OrdinalIgnoreCase) Then
                                        lMethodName = lTokens(j + 1)
                                        Exit For
                                    End If
                                Next
                                Console.WriteLine($"    Found method: {lMethodName} at line {i + 1}")
                            End If
                        ElseIf IsPropertyDeclaration(lTrimmedLine) Then
                            If ParseProperty(lTrimmedLine, lCurrentType, i) Then
                                lPropertiesFound += 1
                                Console.WriteLine($"    Found property at line {i + 1}")
                            End If
                        ElseIf IsEventDeclaration(lTrimmedLine) Then
                            If ParseEvent(lTrimmedLine, lCurrentType, i, vLines) Then
                                Console.WriteLine($"    Found event at line {i + 1}")
                            End If
                        ElseIf IsFieldDeclaration(lTrimmedLine) Then
                            If ParseField(lTrimmedLine, lCurrentType, i, vLines) Then
                                Console.WriteLine($"    Found field at line {i + 1}")
                            End If
                        End If
                    End If
                    
                    i += 1
                End While
                
                Console.WriteLine($"  ParseFileContent complete for {System.IO.Path.GetFileName(vFilePath)}:")
                Console.WriteLine($"    Types: {lTypesFound}, Methods: {lMethodsFound}, Properties: {lPropertiesFound}")
                
            Catch ex As Exception
                Console.WriteLine($"ParseFileContent error in {vFilePath}: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                pParseErrors.Add($"Error in {vFilePath}: {ex.Message}")
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
                
'                Console.WriteLine($"  Added {lMethodType}: {lName} To {vTypeNode.Name}")
                
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
                
'                Console.WriteLine($"  Added Property: {lName} To {vTypeNode.Name}")
                
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
                
'                Console.WriteLine($"ProjectParser: Entered Namespace '{lNamespacePath}'")
                
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
'                    Console.WriteLine($"ProjectParser: Merging Partial type '{vTypeName}' in parent '{vParentNode.Name}'")
                    Return lPartialNode
                End If
                
                ' Check if a non-partial version already exists in the parent
                for each lChild in vParentNode.Children
                    If lChild.NodeType = vNodeType AndAlso
                       String.Equals(lChild.Name, vTypeName, StringComparison.OrdinalIgnoreCase) Then
                        ' Found existing node - mark as partial and use it
                        lChild.IsPartial = True
                        pPartialClasses(lKey) = lChild
'                        Console.WriteLine($"ProjectParser: Converting existing type '{vTypeName}' to partial in parent '{vParentNode.Name}'")
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
                
'                Console.WriteLine($"ProjectParser: Created New Partial type '{vTypeName}' in parent '{vParentNode.Name}'")
                
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
        
        ''' <summary>
        ''' Parses type declarations (Class, Module, Interface, Structure, Enum)
        ''' </summary>
        ''' <param name="vLine">The line containing the declaration</param>
        ''' <param name="vParentNode">The parent node to add to</param>
        ''' <param name="vTypeNode">Output parameter for the created type node</param>
        ''' <param name="vLines">All lines in the file (for multi-line parsing)</param>
        ''' <param name="vLineIndex">Current line index</param>
        ''' <returns>True if successfully parsed</returns>
        ''' <remarks>
        ''' Fixed to properly handle partial classes and use GetOrCreatePartialTypeNode
        ''' </remarks>
        Private Function ParseTypeDeclaration(vLine As String, vParentNode As SyntaxNode, 
                                             ByRef vTypeNode As SyntaxNode, vLines As List(Of String), 
                                             vLineIndex As Integer) As Boolean
            Try
                vTypeNode = Nothing
                If vParentNode Is Nothing Then Return False
                
                ' Store current line number for node creation
                pCurrentLineNumber = vLineIndex
                
                ' Tokenize the line
                Dim lTokens As List(Of String) = TokenizeLine(vLine)
                If lTokens.Count < 2 Then Return False
                
                ' Find the type keyword (Class, Module, Interface, Structure, Enum)
                Dim lTypeKeyword As String = ""
                Dim lTypeIndex As Integer = -1
                Dim lIsPartial As Boolean = False
                
                For i As Integer = 0 To lTokens.Count - 1
                    ' Check for Partial modifier
                    If String.Equals(lTokens(i), "Partial", StringComparison.OrdinalIgnoreCase) Then
                        lIsPartial = True
                        Console.WriteLine($"  Detected 'Partial' modifier at token {i}")
                    End If
                    
                    ' Check for type keywords
                    If IsTypeKeyword(lTokens(i)) Then
                        lTypeKeyword = lTokens(i)
                        lTypeIndex = i
                        Exit For
                    End If
                Next
                
                If lTypeIndex < 0 OrElse lTypeIndex >= lTokens.Count - 1 Then
                    Return False
                End If
                
                ' Get the type name (next token after type keyword)
                Dim lTypeName As String = lTokens(lTypeIndex + 1)
                
                ' Remove any parentheses or generic parameters
                Dim lParenIndex As Integer = lTypeName.IndexOf("(")
                If lParenIndex >= 0 Then
                    lTypeName = lTypeName.Substring(0, lParenIndex)
                End If
                
                Dim lGenericIndex As Integer = lTypeName.IndexOf("(Of")
                If lGenericIndex >= 0 Then
                    lTypeName = lTypeName.Substring(0, lGenericIndex)
                End If
                
                ' Determine node type
                Dim lNodeType As CodeNodeType = GetNodeTypeFromKeyword(lTypeKeyword)
                
                Console.WriteLine($"ParseTypeDeclaration: Found {If(lIsPartial, "Partial ", "")}{lTypeKeyword} '{lTypeName}'")
                
                ' If it's partial, use GetOrCreatePartialTypeNode
                If lIsPartial Then
                    vTypeNode = GetOrCreatePartialTypeNode(vParentNode, lTypeName, lNodeType, pCurrentFile)
                    
                    If vTypeNode Is Nothing Then
                        Console.WriteLine($"  ERROR: Failed to get/create partial type node for '{lTypeName}'")
                        Return False
                    End If
                    
                    Console.WriteLine($"  Got partial type node with {vTypeNode.Children.Count} existing members")
                Else
                    ' Check if a type with this name already exists (might be from a partial)
                    For Each lChild In vParentNode.Children
                        If lChild.NodeType = lNodeType AndAlso
                           String.Equals(lChild.Name, lTypeName, StringComparison.OrdinalIgnoreCase) Then
                            ' Found existing type - could be a partial without the keyword
                            Console.WriteLine($"  Found existing type '{lTypeName}', treating as partial")
                            lChild.IsPartial = True
                            vTypeNode = lChild
                            
                            ' Track in partial classes dictionary
                            Dim lParentPath As String = GetNodePath(vParentNode)
                            Dim lKey As String = $"{lParentPath}:{lTypeName}"
                            pPartialClasses(lKey) = lChild
                            Exit For
                        End If
                    Next
                    
                    ' If no existing type found, create new one
                    If vTypeNode Is Nothing Then
                        vTypeNode = New SyntaxNode(lNodeType, lTypeName)
                        vTypeNode.FilePath = pCurrentFile
                        vTypeNode.StartLine = vLineIndex
                        vTypeNode.IsPartial = False ' Not explicitly partial
                        
                        ' Add to parent
                        vParentNode.AddChild(vTypeNode)
                        Console.WriteLine($"  Created new {lTypeKeyword} '{lTypeName}'")
                    End If
                End If
                
                ' Parse modifiers
                ParseModifiers(vLine, vTypeNode)
                ParseTypeModifiers(vLine, vTypeNode)
                
                ' Parse inheritance and implementation
                ParseInheritanceAndImplementation(vLine, vTypeNode)
                
                ' Extract and apply XML documentation if available
                If vLines IsNot Nothing AndAlso vLineIndex > 0 Then
                    ExtractAndApplyXmlDocumentation(vLines, vLineIndex, vTypeNode)
                End If
                
                Console.WriteLine($"  ParseTypeDeclaration complete for '{lTypeName}'")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ParseTypeDeclaration error: {ex.Message}")
                Console.WriteLine($"  Line: {vLine}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Helper to check if a token is a type keyword
        ''' </summary>
        Private Function IsTypeKeyword(vToken As String) As Boolean
            Return String.Equals(vToken, "Class", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(vToken, "Module", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(vToken, "Interface", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(vToken, "Structure", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(vToken, "Struct", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(vToken, "Enum", StringComparison.OrdinalIgnoreCase)
        End Function

        ''' <summary>
        ''' Helper to get CodeNodeType from keyword
        ''' </summary>
        Private Function GetNodeTypeFromKeyword(vKeyword As String) As CodeNodeType
            Select Case vKeyword.ToLower()
                Case "class"
                    Return CodeNodeType.eClass
                Case "module"
                    Return CodeNodeType.eModule
                Case "interface"
                    Return CodeNodeType.eInterface
                Case "structure", "struct"
                    Return CodeNodeType.eStructure
                Case "enum"
                    Return CodeNodeType.eEnum
                Case Else
                    Return CodeNodeType.eClass
            End Select
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
'                    Console.WriteLine($"  Added {lMethodType}: {lName} To {vTypeNode.Name}")
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
'                    Console.WriteLine($"  Added Single-line Property: {lName} To {vTypeNode.Name}")
                Else
'                    Console.WriteLine($"  Added Property: {lName} To {vTypeNode.Name}")
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
'                Console.WriteLine($"  Added Enum Member: {lName} To {vTypeNode.Name}")
                
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
                
               ' Console.WriteLine($"  Added Field: {lName} To {vTypeNode.Name}")
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
                
               ' Console.WriteLine($"  Added Event: {lName} To {vTypeNode.Name}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseEvent error: {ex.Message}")
                Return False
            End Try
        End Function

    End Class
    
End Namespace