' RoslynProjectParser.vb - Complete replacement for ProjectParser using Roslyn
' This REPLACES the old ProjectParser completely
' Created: 2025-01-01

Imports System
Imports System.IO
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities
Imports SimpleIDE.Parsers

' Add these aliases to resolve ambiguity:
Imports RoslynSyntaxNode = Microsoft.CodeAnalysis.SyntaxNode
Imports SimpleSyntaxNode = SimpleIDE.Syntax.SyntaxNode
Imports SimpleSyntaxToken = SimpleIDE.Models.SyntaxToken
Imports MSBuildWorkspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace
Imports AdhocWorkspace = Microsoft.CodeAnalysis.AdhocWorkspace
Imports Project = Microsoft.CodeAnalysis.Project
Imports RoslynProjectInfo = Microsoft.CodeAnalysis.ProjectInfo
Imports SimpleProjectInfo = SimpleIDE.Managers.ProjectInfo

Namespace Managers
    
    ''' <summary>
    ''' Parses VB.NET projects using Roslyn compiler services
    ''' </summary>
    ''' <remarks>
    ''' This is a complete replacement for the old ProjectParser class.
    ''' It provides the same interface but uses Roslyn for accurate parsing.
    ''' </remarks>
    Public Class ProjectParser
        
        ' ===== Private Fields =====
        Private pProjectManager As ProjectManager
        Private pWorkspace As AdhocWorkspace
        Private pCompilation As Compilation
        Private pParseOptions As VisualBasicParseOptions
        Private pCompilationOptions As VisualBasicCompilationOptions
        Private pConverter As RoslynConverter
        Private pProjectTree As ProjectSyntaxTree
        Private pParseErrors As New List(Of String)()
        Private pIsInitialized As Boolean = False
        Private pPartialClasses As Dictionary(Of String, SimpleSyntaxNode)
        Private pRootNamespace As String
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets the parse options used for parsing
        ''' </summary>
        Public ReadOnly Property ParseOptions As VisualBasicParseOptions
            Get
                Return pParseOptions
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the current compilation
        ''' </summary>
        Public ReadOnly Property Compilation As Compilation
            Get
                Return pCompilation
            End Get
        End Property
        
        ''' <summary>
        ''' Gets parse errors from the last operation
        ''' </summary>
        Public ReadOnly Property ParseErrors As List(Of String)
            Get
                Return pParseErrors
            End Get
        End Property
        
        ' ===== Constructor =====
        
        Public Sub New(vProjectManager As ProjectManager)
            Try
                If vProjectManager Is Nothing Then
                    Throw New ArgumentNullException(NameOf(vProjectManager))
                End If
                
                pProjectManager = vProjectManager
                
                ' Initialize basic fields first
                pParseErrors = New List(Of String)()
                pPartialClasses = New Dictionary(Of String, SimpleSyntaxNode)(StringComparer.OrdinalIgnoreCase)
                pConverter = New RoslynConverter()
                pWorkspace = New AdhocWorkspace()
                
                ' Get root namespace from project
                pRootNamespace = If(vProjectManager.RootNamespace, "SimpleIDE")
                
                ' Call Initialize to set up Roslyn components
                Initialize()
                
                Console.WriteLine($"ProjectParser created with root namespace: {pRootNamespace}")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser constructor error: {ex.Message}")
                ' Ensure minimal initialization even if constructor fails
                If pParseErrors Is Nothing Then pParseErrors = New List(Of String)()
                If pConverter Is Nothing Then pConverter = New RoslynConverter()
                If pParseOptions Is Nothing Then pParseOptions = VisualBasicParseOptions.Default
            End Try
        End Sub

        
        ' ===== Initialization =====
        
        ''' <summary>
        ''' Initializes the parser with Roslyn components
        ''' </summary>
        Private Sub Initialize()
            Try
                Console.WriteLine("ProjectParser: Initializing Roslyn components...")
                
                ' Create workspace
                pWorkspace = New AdhocWorkspace()
                
                ' Setup parse options (VB.NET 15.5 which corresponds to Visual Basic 2017)
                pParseOptions = VisualBasicParseOptions.Default.
                    WithLanguageVersion(LanguageVersion.VisualBasic15_5).
                    WithDocumentationMode(DocumentationMode.Parse)
                
                ' Setup compilation options with proper GlobalImports
                Dim lGlobalImports As New List(Of GlobalImport)()
                lGlobalImports.Add(GlobalImport.Parse("System"))
                lGlobalImports.Add(GlobalImport.Parse("System.Collections.Generic"))
                lGlobalImports.Add(GlobalImport.Parse("System.Linq"))
                
                pCompilationOptions = New VisualBasicCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    rootNamespace:=pProjectManager.RootNamespace,
                    globalImports:=lGlobalImports,
                    optionStrict:=OptionStrict.Off,
                    optionInfer:=True,
                    optionExplicit:=True,
                    optionCompareText:=False
                )
                
                ' Create empty compilation with assembly name from CurrentProjectInfo
                Dim lAssemblyName As String = If(pProjectManager.CurrentProjectInfo?.AssemblyName, 
                                                  pProjectManager.CurrentProjectName,
                                                  "SimpleIDE")
                
                pCompilation = VisualBasicCompilation.Create(
                    lAssemblyName,
                    syntaxTrees:=Array.Empty(Of Microsoft.CodeAnalysis.SyntaxTree)(),
                    references:=GetDefaultReferences(),
                    options:=pCompilationOptions
                )
                
                ' Create converter
                pConverter = New RoslynConverter()
                
                ' Create project tree with assembly name from CurrentProjectInfo
                pProjectTree = New ProjectSyntaxTree() with {
                    .RootNamespace = pProjectManager.RootNamespace,
                    .AssemblyName = If(pProjectManager.CurrentProjectInfo?.AssemblyName, 
                                       pProjectManager.CurrentProjectName,
                                       "SimpleIDE")
                }
                
                pIsInitialized = True
                Console.WriteLine("ProjectParser: Initialization complete")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.Initialize error: {ex.Message}")
                pIsInitialized = False
            End Try
        End Sub

        ''' <summary>
        ''' Parses the current project using the ProjectManager's current project file
        ''' </summary>
        ''' <returns>True if parsing succeeded</returns>
        Public Function ParseProject() As Boolean
            Try
                ' Use the project file from ProjectManager
                If pProjectManager IsNot Nothing AndAlso 
                   Not String.IsNullOrEmpty(pProjectManager.ProjectFile) Then
                    Return ParseProject(pProjectManager.ProjectFile)
                End If
                
                Console.WriteLine("ProjectParser.ParseProject: No project file available")
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseProject error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Parses an entire project file
        ''' </summary>
        ''' <param name="vProjectFilePath">Path to the .vbproj file</param>
        Public Function ParseProject(vProjectFilePath As String) As Boolean
            Try
                If Not pIsInitialized Then Initialize()
                
                Console.WriteLine($"ProjectParser: Parsing project {vProjectFilePath}")
                pParseErrors.Clear()
                
                ' Get all source files from project manager
                Dim lSourceFiles = pProjectManager.GetProjectSourceFiles()
                Console.WriteLine($"Found {lSourceFiles.Count} source files")
                
                ' Parse all files
                Dim lProjectTree As New SimpleSyntaxNode(CodeNodeType.eProject, 
                    If(pProjectManager.CurrentProjectInfo?.AssemblyName, 
                       pProjectManager.CurrentProjectName,
                       "SimpleIDE"))
                lProjectTree.FilePath = pProjectManager.ProjectFile
                
                ' Create root namespace node if needed
                Dim lRootNamespace = pProjectManager.RootNamespace
                Dim lNamespaceNode As SimpleSyntaxNode = Nothing
                
                If Not String.IsNullOrEmpty(lRootNamespace) Then
                    lNamespaceNode = New SimpleSyntaxNode(CodeNodeType.eNamespace, lRootNamespace)
                    lNamespaceNode.IsImplicit = True
                    lProjectTree.AddChild(lNamespaceNode)
                End If
                
                ' Parse each file
                Dim lSuccessCount As Integer = 0
                for each lFilePath in lSourceFiles
                    Dim lSourceFile = pProjectManager.GetSourceFileInfo(lFilePath)
                    If lSourceFile IsNot Nothing Then
                        Dim lFileSyntaxTree = ParseSourceFile(lSourceFile)
                        If lFileSyntaxTree IsNot Nothing Then
                            pProjectTree.Files(lFilePath) = lFileSyntaxTree
                            
                            ' Add to project tree
                            If lFileSyntaxTree.SimpleIDETree IsNot Nothing Then
                                If lNamespaceNode IsNot Nothing Then
                                    MergeFileIntoNamespace(lFileSyntaxTree.SimpleIDETree, lNamespaceNode)
                                Else
                                    MergeFileIntoNamespace(lFileSyntaxTree.SimpleIDETree, lProjectTree)
                                End If
                            End If
                            
                            lSuccessCount += 1
                        End If
                    End If
                Next
                
                pProjectTree.RootNode = lProjectTree
                
                Console.WriteLine($"ProjectParser: Successfully parsed {lSuccessCount}/{lSourceFiles.Count} files")
                Return lSuccessCount > 0
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseProject error: {ex.Message}")
                Console.WriteLine($"  Error type: {ex.GetType().FullName}")
                Console.WriteLine($"  Stack trace: {ex.StackTrace}")
                pParseErrors.Add($"Project parse error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parses a single source file
        ''' </summary>
        ''' <param name="vSourceFile">The source file to parse</param>
        ''' <returns>The parsed FileSyntaxTree</returns>
        Public Function ParseSourceFile(vSourceFile As SourceFileInfo) As FileSyntaxTree
            Try
                If vSourceFile Is Nothing Then Return Nothing
                
                Console.WriteLine($"ProjectParser: Parsing {vSourceFile.FileName}...")
                
                ' Parse with Roslyn
                Dim lRoslynTree = VisualBasicSyntaxTree.ParseText(
                    vSourceFile.TextContent,
                    options:=pParseOptions,
                    path:=vSourceFile.FilePath
                )
                
                ' Get semantic model if compilation available
                Dim lSemanticModel As SemanticModel = Nothing
                If pCompilation IsNot Nothing Then
                    lSemanticModel = pCompilation.GetSemanticModel(lRoslynTree)
                End If
                
                ' Convert to SimpleIDE format
                Dim lSimpleIDETree = pConverter.ConvertToSimpleIDE(lRoslynTree, vSourceFile.FilePath)
                
                ' Build line metadata for syntax highlighting
                Dim lLineMetadata = BuildLineMetadata(lRoslynTree, vSourceFile)
                
                ' Update SourceFileInfo with parse results
                ' Note: LineMetadata is a read-only property that returns an array
                ' We need to update the internal storage instead
                If vSourceFile.LineMetadata IsNot Nothing AndAlso lLineMetadata IsNot Nothing Then
                    ' Copy the new metadata to the existing array
                    for i = 0 To Math.Min(lLineMetadata.Length - 1, vSourceFile.LineMetadata.Length - 1)
                        vSourceFile.LineMetadata(i) = lLineMetadata(i)
                    Next
                End If
                
                vSourceFile.SyntaxTree = lSimpleIDETree
                vSourceFile.ParseResult = lSimpleIDETree
                
                ' Update CharacterColors from LineMetadata
                If lLineMetadata IsNot Nothing Then
                    ReDim vSourceFile.CharacterColors(lLineMetadata.Length - 1)
                    
                    for i = 0 To lLineMetadata.Length - 1
                        If lLineMetadata(i) IsNot Nothing AndAlso lLineMetadata(i).CharacterColors IsNot Nothing Then
                            vSourceFile.CharacterColors(i) = lLineMetadata(i).CharacterColors
                        Else
                            vSourceFile.CharacterColors(i) = New Byte() {}
                        End If
                    Next
                End If
                
                ' Get diagnostics
                Dim lDiagnostics = lRoslynTree.GetDiagnostics().ToList()
                
                ' Log any errors
                for each lDiag in lDiagnostics
                    If lDiag.Severity = DiagnosticSeverity.error Then
                        pParseErrors.Add($"{vSourceFile.FileName}: {lDiag.GetMessage()}")
                    End If
                Next
                
                ' Create file syntax tree
                Return New FileSyntaxTree with {
                    .FilePath = vSourceFile.FilePath,
                    .RoslynTree = lRoslynTree,
                    .SimpleIDETree = lSimpleIDETree,
                    .SemanticModel = lSemanticModel,
                    .LineMetadata = lLineMetadata,
                    .Diagnostics = lDiagnostics
                }
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseSourceFile error in {vSourceFile?.FileName}: {ex.Message}")
                pParseErrors.Add($"Error parsing {vSourceFile?.FileName}: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Parses a single line of code
        ''' </summary>
        ''' <param name="vLine">The line text to parse</param>
        ''' <param name="vLineNumber">The line number (0-based)</param>
        Public Function ParseLine(vLine As String, vLineNumber As Integer) As LineMetadata
            Try
                If String.IsNullOrEmpty(vLine) Then
                    Return New LineMetadata() with {
                        .LineNumber = vLineNumber,
                        .LineText = "",
                        .Length = 0
                    }
                End If
                
                ' Parse the single line as a syntax tree
                Dim lTree = VisualBasicSyntaxTree.ParseText(vLine, pParseOptions)
                Dim lRoot = lTree.GetRoot()
                
                ' Create line metadata
                Dim lMetadata As New LineMetadata() with {
                    .LineNumber = vLineNumber,
                    .LineText = vLine,
                    .Length = vLine.Length
                }
                
                ' Process tokens to build syntax highlighting
                ProcessTokensForLine(lRoot, lMetadata)
                
                Return lMetadata
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseLine error: {ex.Message}")
                Return New LineMetadata() with {
                    .LineNumber = vLineNumber,
                    .LineText = vLine,
                    .Length = vLine.Length
                }
            End Try
        End Function
        
        ''' <summary>
        ''' Parses content directly without a source file
        ''' </summary>
        ''' <param name="vContent">The VB.NET code content to parse</param>
        ''' <param name="vFilePath">Optional file path for context</param>
        Public Function ParseContent(vContent As String, vFilePath As String) As SimpleSyntaxNode
            Try
                ' Add defensive check
                If pParseOptions Is Nothing Then
                    Console.WriteLine($"ParseContent: pParseOptions is null, initializing...")
                    pParseOptions = VisualBasicParseOptions.Default.
                        WithLanguageVersion(LanguageVersion.VisualBasic15_5).
                        WithDocumentationMode(DocumentationMode.Parse)
                End If
                
                ' Check for null converter
                If pConverter Is Nothing Then
                    Console.WriteLine($"ParseContent: pConverter is null, creating...")
                    pConverter = New RoslynConverter()
                End If
                
                ' Parse using Roslyn
                Dim lTree = VisualBasicSyntaxTree.ParseText(
                    vContent, 
                    options:=pParseOptions,
                    path:=vFilePath
                )
                
                ' Convert to SimpleIDE format
                Return pConverter.ConvertToSimpleIDE(lTree, vFilePath)
                
            Catch ex As Exception
                Console.WriteLine($"ParseContent error: {ex.Message}")
                Console.WriteLine($"  Stack trace: {ex.StackTrace}")
                If pParseErrors IsNot Nothing Then
                    pParseErrors.Add($"Parse error in {vFilePath}: {ex.Message}")
                End If
                Return Nothing
            End Try
        End Function       
 
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Gets default .NET references
        ''' </summary>
        Private Function GetDefaultReferences() As List(Of MetadataReference)
            Try
                Dim lReferences As New List(Of MetadataReference)()
                
                ' Add mscorlib
                lReferences.Add(MetadataReference.CreateFromFile(GetType(Object).Assembly.Location))
                
                ' Add System
                lReferences.Add(MetadataReference.CreateFromFile(GetType(System.Console).Assembly.Location))
                
                ' Add System.Core
                lReferences.Add(MetadataReference.CreateFromFile(GetType(System.Linq.Enumerable).Assembly.Location))
                
                ' Add Microsoft.VisualBasic
                lReferences.Add(MetadataReference.CreateFromFile(GetType(Microsoft.VisualBasic.Constants).Assembly.Location))
                
                Return lReferences
                
            Catch ex As Exception
                Console.WriteLine($"GetDefaultReferences error: {ex.Message}")
                Return New List(Of MetadataReference)()
            End Try
        End Function
        
        ''' <summary>
        ''' Builds line metadata for syntax highlighting
        ''' </summary>
        Private Function BuildLineMetadata(vRoslynTree As Microsoft.CodeAnalysis.SyntaxTree, vSourceFile As SourceFileInfo) As LineMetadata()
            Try
                Dim lLineCount = vSourceFile.TextLines.Count
                Dim lLineMetadata(lLineCount - 1) As LineMetadata
                
                ' Initialize metadata for each line
                for i = 0 To lLineCount - 1
                    lLineMetadata(i) = New LineMetadata() with {
                        .LineNumber = i,
                        .LineText = vSourceFile.TextLines(i),
                        .Length = vSourceFile.TextLines(i).Length
                    }
                Next
                
                ' Process Roslyn tree to extract tokens
                Dim lRoot = vRoslynTree.GetRoot()
                Dim lLineTokens As New Dictionary(Of Integer, List(Of SimpleSyntaxToken))()
                
                ' Process all tokens
                ProcessNodeForTokens(lRoot, lLineTokens, vSourceFile)
                
                ' Apply tokens to line metadata
                for each lKvp in lLineTokens
                    Dim lLineIndex = lKvp.Key
                    If lLineIndex >= 0 AndAlso lLineIndex < lLineCount Then
                        lLineMetadata(lLineIndex).SyntaxTokens = lKvp.Value
                        
                        ' Convert to character colors
                        ConvertTokensToCharacterColors(lLineMetadata(lLineIndex))
                    End If
                Next
                
                Return lLineMetadata
                
            Catch ex As Exception
                Console.WriteLine($"BuildLineMetadata error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Processes a Roslyn node to extract syntax tokens
        ''' </summary>
        Private Sub ProcessNodeForTokens(vNode As RoslynSyntaxNode, vLineTokens As Dictionary(Of Integer, List(Of SimpleSyntaxToken)), vSourceFile As SourceFileInfo)
            Try
                If vNode Is Nothing Then Return
                
                ' Process tokens
                for each lToken in vNode.DescendantTokens()
                    If lToken.Kind() <> SyntaxKind.EndOfFileToken AndAlso
                       lToken.Kind() <> SyntaxKind.None Then
                        
                        ' Get line number
                        Dim lLineSpan = lToken.GetLocation().GetLineSpan()
                        Dim lLine = lLineSpan.StartLinePosition.Line
                        
                        ' Get or create token list for this line
                        Dim lTokenList As List(Of SimpleSyntaxToken) = Nothing
                        If Not vLineTokens.TryGetValue(lLine, lTokenList) Then
                            lTokenList = New List(Of SimpleSyntaxToken)()
                            vLineTokens(lLine) = lTokenList
                        End If
                        
                        ' Create syntax token
                        Dim lStartCol = lLineSpan.StartLinePosition.Character
                        Dim lEndCol = lLineSpan.EndLinePosition.Character
                        
                        ' Handle multi-line tokens
                        If lLineSpan.StartLinePosition.Line <> lLineSpan.EndLinePosition.Line Then
                            ' For now, just handle the first line
                            lEndCol = vSourceFile.TextLines(lLine).Length
                        End If
                        
                        Dim lSyntaxToken As New SimpleSyntaxToken() with {
                            .StartColumn = lStartCol,
                            .Length = Math.Max(1, lEndCol - lStartCol),
                            .TokenType = ConvertToSyntaxTokenType(lToken.Kind())
                        }
                        
                        lTokenList.Add(lSyntaxToken)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessNodeForTokens error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Converts Roslyn SyntaxKind to SimpleIDE TokenType
        ''' </summary>
        Private Function ConvertTokenType(vKind As SyntaxKind) As TokenType
            Select Case vKind
                ' Keywords
                Case SyntaxKind.ClassKeyword, SyntaxKind.ModuleKeyword, SyntaxKind.InterfaceKeyword,
                     SyntaxKind.StructureKeyword, SyntaxKind.EnumKeyword, SyntaxKind.DelegateKeyword,
                     SyntaxKind.SubKeyword, SyntaxKind.FunctionKeyword, SyntaxKind.PropertyKeyword,
                     SyntaxKind.EventKeyword, SyntaxKind.OperatorKeyword, SyntaxKind.PublicKeyword,
                     SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword, SyntaxKind.FriendKeyword,
                     SyntaxKind.SharedKeyword, SyntaxKind.StaticKeyword, SyntaxKind.DimKeyword,
                     SyntaxKind.ConstKeyword, SyntaxKind.IfKeyword, SyntaxKind.ThenKeyword,
                     SyntaxKind.ElseKeyword, SyntaxKind.ElseIfKeyword, SyntaxKind.EndKeyword,
                     SyntaxKind.SelectKeyword, SyntaxKind.CaseKeyword, SyntaxKind.WhileKeyword,
                     SyntaxKind.DoKeyword, SyntaxKind.LoopKeyword, SyntaxKind.ForKeyword,
                     SyntaxKind.ToKeyword, SyntaxKind.StepKeyword, SyntaxKind.NextKeyword,
                     SyntaxKind.ReturnKeyword, SyntaxKind.TryKeyword, SyntaxKind.CatchKeyword,
                     SyntaxKind.FinallyKeyword, SyntaxKind.ThrowKeyword, SyntaxKind.WithKeyword,
                     SyntaxKind.UsingKeyword, SyntaxKind.ImportsKeyword, SyntaxKind.AsKeyword,
                     SyntaxKind.NewKeyword, SyntaxKind.MeKeyword, SyntaxKind.MyBaseKeyword,
                     SyntaxKind.MyClassKeyword, SyntaxKind.NothingKeyword, SyntaxKind.TrueKeyword,
                     SyntaxKind.FalseKeyword, SyntaxKind.InheritsKeyword, SyntaxKind.ImplementsKeyword
                    Return TokenType.eKeyword
                    
                ' Identifiers
                Case SyntaxKind.IdentifierToken
                    Return TokenType.eIdentifier
                    
                ' Strings
                Case SyntaxKind.StringLiteralToken, SyntaxKind.CharacterLiteralToken,
                     SyntaxKind.InterpolatedStringTextToken
                    Return TokenType.eStringLiteral
                    
                ' Numbers
                Case SyntaxKind.IntegerLiteralToken, SyntaxKind.DecimalLiteralToken,
                     SyntaxKind.FloatingLiteralToken, SyntaxKind.DateLiteralToken
                    Return TokenType.eNumber
                    
                ' Operators
                Case SyntaxKind.PlusToken, SyntaxKind.MinusToken, SyntaxKind.AsteriskToken,
                     SyntaxKind.SlashToken, SyntaxKind.BackslashToken, SyntaxKind.CaretToken,
                     SyntaxKind.AmpersandToken, SyntaxKind.EqualsToken, SyntaxKind.LessThanToken,
                     SyntaxKind.GreaterThanToken, SyntaxKind.LessThanEqualsToken,
                     SyntaxKind.GreaterThanEqualsToken, SyntaxKind.LessThanGreaterThanToken,
                     SyntaxKind.ColonEqualsToken, SyntaxKind.PlusEqualsToken, SyntaxKind.MinusEqualsToken,
                     SyntaxKind.AsteriskEqualsToken, SyntaxKind.SlashEqualsToken,
                     SyntaxKind.BackslashEqualsToken, SyntaxKind.CaretEqualsToken,
                     SyntaxKind.AmpersandEqualsToken, SyntaxKind.LessThanLessThanToken,
                     SyntaxKind.GreaterThanGreaterThanToken, SyntaxKind.LessThanLessThanEqualsToken,
                     SyntaxKind.GreaterThanGreaterThanEqualsToken
                    Return TokenType.eOperator
                    
                ' Punctuation/Delimiters
                Case SyntaxKind.CommaToken, SyntaxKind.DotToken, SyntaxKind.OpenParenToken,
                     SyntaxKind.CloseParenToken, SyntaxKind.OpenBraceToken, SyntaxKind.CloseBraceToken,
                     SyntaxKind.SemicolonToken, SyntaxKind.ColonToken, SyntaxKind.QuestionToken
                    Return TokenType.eDelimiter
                    
                ' Whitespace
                Case SyntaxKind.WhitespaceTrivia, SyntaxKind.EndOfLineTrivia
                    Return TokenType.eWhitespace
                    
                ' Comments
                Case SyntaxKind.CommentTrivia, SyntaxKind.DocumentationCommentExteriorTrivia,
                     SyntaxKind.DocumentationCommentTrivia
                    Return TokenType.eComment
                    
                Case Else
                    Return TokenType.eOther
            End Select
        End Function
        
        ''' <summary>
        ''' Converts to SyntaxTokenType for syntax highlighting
        ''' </summary>
        Private Function ConvertToSyntaxTokenType(vKind As SyntaxKind) As SyntaxTokenType
            Dim lTokenType = ConvertTokenType(vKind)
            
            Select Case lTokenType
                Case TokenType.eKeyword
                    Return SyntaxTokenType.eKeyword
                Case TokenType.eIdentifier
                    Return SyntaxTokenType.eIdentifier
                Case TokenType.eStringLiteral, TokenType.eString
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
        
        ' Additional helper methods continue...
        
        ''' <summary>
        ''' Converts syntax tokens to character colors for rendering
        ''' </summary>
        Private Sub ConvertTokensToCharacterColors(vLineMetadata As LineMetadata)
            Try
                If vLineMetadata Is Nothing OrElse vLineMetadata.Length = 0 Then Return
                
                ' Initialize character colors array
                ReDim vLineMetadata.CharacterColors(vLineMetadata.Length - 1)
                
                ' Set default color for all characters
                for i = 0 To vLineMetadata.Length - 1
                    vLineMetadata.CharacterColors(i) = CByte(SyntaxTokenType.eNormal)
                Next
                
                ' Apply token colors
                If vLineMetadata.SyntaxTokens IsNot Nothing Then
                    for each lToken in vLineMetadata.SyntaxTokens
                        Dim lStartCol = Math.Max(0, lToken.StartColumn)
                        Dim lEndCol = Math.Min(vLineMetadata.Length - 1, lToken.StartColumn + lToken.Length - 1)
                        
                        for i = lStartCol To lEndCol
                            vLineMetadata.CharacterColors(i) = CByte(lToken.TokenType)
                        Next
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ConvertTokensToCharacterColors error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes tokens for a single line
        ''' </summary>
        Private Sub ProcessTokensForLine(vNode As RoslynSyntaxNode, vLineMetadata As LineMetadata)
            Try
                If vNode Is Nothing OrElse vLineMetadata Is Nothing Then Return
                
                vLineMetadata.SyntaxTokens = New List(Of SimpleSyntaxToken)()
                
                ' Process all tokens
                for each lToken in vNode.DescendantTokens()
                    If lToken.Kind() <> SyntaxKind.EndOfFileToken AndAlso
                       lToken.Kind() <> SyntaxKind.None Then
                        
                        Dim lSyntaxToken As New SimpleSyntaxToken() with {
                            .StartColumn = lToken.SpanStart,
                            .Length = lToken.Span.Length,
                            .TokenType = ConvertToSyntaxTokenType(lToken.Kind())
                        }
                        
                        vLineMetadata.SyntaxTokens.Add(lSyntaxToken)
                    End If
                Next
                
                ' Convert to character colors
                ConvertTokensToCharacterColors(vLineMetadata)
                
            Catch ex As Exception
                Console.WriteLine($"ProcessTokensForLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Merges a file's syntax tree into the project namespace
        ''' </summary>
        Private Sub MergeFileIntoNamespace(vFileNode As SimpleSyntaxNode, vNamespaceNode As SimpleSyntaxNode)
            Try
                If vFileNode Is Nothing OrElse vNamespaceNode Is Nothing Then Return
                
                ' Add all top-level types from the file to the namespace
                for each lChild in vFileNode.Children
                    Select Case lChild.NodeType
                        Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eInterface,
                             CodeNodeType.eStructure, CodeNodeType.eEnum, CodeNodeType.eDelegate
                            vNamespaceNode.AddChild(lChild)
                            
                        Case CodeNodeType.eNamespace
                            ' Merge namespace contents
                            MergeNamespaceContents(lChild, vNamespaceNode)
                    End Select
                Next
                
            Catch ex As Exception
                Console.WriteLine($"MergeFileIntoNamespace error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Merges namespace contents
        ''' </summary>
        Private Sub MergeNamespaceContents(vSourceNamespace As SimpleSyntaxNode, vTargetNamespace As SimpleSyntaxNode)
            Try
                ' If same namespace, merge children
                If vSourceNamespace.Name = vTargetNamespace.Name Then
                    for each lChild in vSourceNamespace.Children
                        vTargetNamespace.AddChild(lChild)
                    Next
                Else
                    ' Different namespace, add as child
                    vTargetNamespace.AddChild(vSourceNamespace)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNamespaceContents error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace