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
Imports MSBuildWorkspace = Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace
Imports AdhocWorkspace = Microsoft.CodeAnalysis.AdhocWorkspace
Imports Project = Microsoft.CodeAnalysis.Project

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
        Private pProject As Project
        Private pCompilation As VisualBasicCompilation
        Private pConverter As RoslynConverter
        Private pRootNamespace As String
        Private pParseErrors As New List(Of String)
        Private pProjectSyntaxTree As SimpleSyntaxNode
        Private pPartialClasses As Dictionary(Of String, SimpleSyntaxNode)
        
        ' ===== Events (Same as old ProjectParser) =====
        
        ''' <summary>
        ''' Raised when parsing of the project structure is completed
        ''' </summary>
        ''' <param name="vRootNode">The root node of the parsed syntax tree</param>
        Public Event ParseCompleted(vRootNode As SimpleSyntaxNode)
        
        ''' <summary>
        ''' Raised when the project structure has been loaded
        ''' </summary>
        ''' <param name="vRootNode">The root node of the project structure</param>
        Public Event ProjectStructureLoaded(vRootNode As SimpleSyntaxNode)
        
        ' ===== Properties (Compatibility with old parser) =====
        
        ''' <summary>
        ''' Gets or sets the line metadata array for syntax highlighting and structure
        ''' </summary>
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
            pConverter = New RoslynConverter()
            pWorkspace = New AdhocWorkspace()
            pPartialClasses = New Dictionary(Of String, SyntaxNode)(StringComparer.OrdinalIgnoreCase)
            pParseErrors = New List(Of String)()
            
            ' Get root namespace from project
            pRootNamespace = If(vProjectManager.RootNamespace, "SimpleIDE")
        End Sub
        
        ' ===== Public Methods (Same interface as old parser) =====
        
        ''' <summary>
        ''' Parses all source files in the project into a unified syntax tree
        ''' </summary>
        ''' <returns>The root SyntaxNode of the project tree</returns>
        Public Function ParseProject() As SimpleSyntaxNode
            Try
                Console.WriteLine("ProjectParser: Starting Roslyn-based project parse...")
                
                ' Create project in workspace
                Dim lProjectInfo = ProjectInfo.Create(
                    ProjectId.CreateNewId(),
                    VersionStamp.Create(),
                    If(pProjectManager.ProjectFile, "Project"),
                    pRootNamespace,
                    LanguageNames.VisualBasic
                )
                
                pProject = pWorkspace.AddProject(lProjectInfo)
                
                ' Add references
                AddProjectReferences()
                
                ' Get all source files from ProjectManager
                Dim lSourceFiles = pProjectManager.SourceFiles
                
                Console.WriteLine($"ProjectParser: Retrieved {If(lSourceFiles?.Count, 0)} source files from ProjectManager")
                
                If lSourceFiles Is Nothing OrElse lSourceFiles.Count = 0 Then
                    Console.WriteLine("ProjectParser: No source files to parse")
                    Return Nothing
                End If
                
                ' Parse all files and build unified tree
                Dim lFileTrees As New Dictionary(Of String, FileSyntaxTree)
                
                for each lKvp in lSourceFiles
                    Dim lFilePath = lKvp.Key
                    Dim lSourceFile = lKvp.Value
                    
                    If lSourceFile.IsLoaded Then
                        Dim lFileTree = ParseSourceFile(lSourceFile)
                        If lFileTree IsNot Nothing Then
                            lFileTrees(lFilePath) = lFileTree
                        End If
                    End If
                Next
                
                ' Create compilation for semantic analysis
                If pProject IsNot Nothing Then
                    Dim lTask = pProject.GetCompilationAsync()
                    lTask.Wait()
                    pCompilation = DirectCast(lTask.Result, VisualBasicCompilation)
                End If
                
                ' Build unified project tree
                pProjectSyntaxTree = BuildUnifiedProjectTree(lFileTrees)
                
                Console.WriteLine($"ProjectParser: Parse completed, {lFileTrees.Count} files processed")
                
                ' Raise events
                If pProjectSyntaxTree IsNot Nothing Then
                    RaiseEvent ParseCompleted(pProjectSyntaxTree)
                    RaiseEvent ProjectStructureLoaded(pProjectSyntaxTree)
                End If
                
                Return pProjectSyntaxTree
                
            Catch ex As Exception
                Console.WriteLine($"ProjectParser.ParseProject error: {ex.Message}")
                pParseErrors.Add($"Project parse error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Parses a single source file
        ''' </summary>
        ''' <param name="vSourceFile">The source file to parse</param>
        ''' <returns>FileSyntaxTree with parse results</returns>
        Private Function ParseSourceFile(vSourceFile As SourceFileInfo) As FileSyntaxTree
            Try
                If vSourceFile Is Nothing Then Return Nothing
                If Not vSourceFile.IsLoaded Then vSourceFile.LoadContent()
                
                Console.WriteLine($"ProjectParser: Parsing {vSourceFile.FileName} with Roslyn")
                
                ' Parse with Roslyn
                Dim lParseOptions = New VisualBasicParseOptions(
                    languageVersion:=LanguageVersion.Latest,
                    documentationMode:=DocumentationMode.Parse
                )
                
                Dim lRoslynTree = VisualBasicSyntaxTree.ParseText(
                    vSourceFile.TextContent,
                    lParseOptions,
                    vSourceFile.FilePath
                )
                
                ' Add to project for semantic analysis
                If pProject IsNot Nothing Then
                    pProject = pProject.AddDocument(
                        vSourceFile.FileName,
                        vSourceFile.TextContent,
                        filePath:=vSourceFile.FilePath
                    ).Project
                End If
                
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
                vSourceFile.LineMetadata = lLineMetadata
                vSourceFile.SyntaxTree = lSimpleIDETree
                vSourceFile.ParseResult = lSimpleIDETree
                
                ' Update CharacterColors from LineMetadata
                If lLineMetadata IsNot Nothing Then
                    ReDim vSourceFile.CharacterColors(lLineMetadata.Length - 1)
                    
                    for i = 0 To lLineMetadata.Length - 1
                        If lLineMetadata(i)?.CharacterColors IsNot Nothing Then
                            vSourceFile.CharacterColors(i) = lLineMetadata(i).CharacterColors
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
        
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Adds project references for compilation
        ''' </summary>
        Private Sub AddProjectReferences()
            Try
                Dim lReferences As New List(Of MetadataReference)
                
                ' Add core references
                lReferences.Add(MetadataReference.CreateFromFile(GetType(Object).Assembly.Location))
                lReferences.Add(MetadataReference.CreateFromFile(GetType(Console).Assembly.Location))
                lReferences.Add(MetadataReference.CreateFromFile(GetType(Linq.Enumerable).Assembly.Location))
                lReferences.Add(MetadataReference.CreateFromFile(GetType(Microsoft.VisualBasic.Constants).Assembly.Location))
                
                ' Add project-specific references
                If pProjectManager.CurrentProjectInfo IsNot Nothing Then
                    for each lRef in pProjectManager.CurrentProjectInfo.References
                        Try
                            lReferences.Add(MetadataReference.CreateFromFile(lRef))
                        Catch
                            ' Skip invalid references
                        End Try
                    Next
                End If
                
                ' Update project with references
                pProject = pProject.WithMetadataReferences(lReferences)
                
            Catch ex As Exception
                Console.WriteLine($"AddProjectReferences error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Builds line metadata from Roslyn syntax tree
        ''' </summary>
        Private Function BuildLineMetadata(vRoslynTree As Microsoft.CodeAnalysis.SyntaxTree, vSourceFile As SourceFileInfo) As LineMetadata()
            Try
                Dim lLines = vSourceFile.TextLines
                Dim lMetadata(lLines.Count - 1) As LineMetadata
                
                for i = 0 To lLines.Count - 1
                    lMetadata(i) = New LineMetadata()
                    lMetadata(i).LineNumber = i
                    lMetadata(i).LineText = lLines(i)
                    lMetadata(i).ParseState = LineParseState.eParsed
                    lMetadata(i).IndentLevel = CalculateIndentLevel(lLines(i))
                    
                    ' Get tokens for this line from Roslyn
                    Dim lLineStart = GetLineStartPosition(vSourceFile.TextContent, i)
                    Dim lLineEnd = lLineStart + lLines(i).Length
                    
                    Dim lTokens = vRoslynTree.GetRoot().DescendantTokens(
                        New Microsoft.CodeAnalysis.Text.TextSpan(lLineStart, lLineEnd - lLineStart)
                    )
                    
                    ' Convert Roslyn tokens to SyntaxTokens
                    lMetadata(i).SyntaxTokens = ConvertTokensForLine(lTokens, lLineStart)
                    
                    ' Generate character tokens for rendering
                    lMetadata(i).CharacterColors = pConverter.ConvertToCharacterTokens(
                        lTokens,
                        lLines(i).Length
                    )
                    
                    ' Check for fold regions (for CustomDrawingEditor)
                    DetectFoldRegions(vRoslynTree.GetRoot(), i, lMetadata(i))
                Next
                
                Return lMetadata
                
            Catch ex As Exception
                Console.WriteLine($"BuildLineMetadata error: {ex.Message}")
                Return New LineMetadata() {}
            End Try
        End Function
        
        ''' <summary>
        ''' Detects fold regions for a line (for CustomDrawingEditor)
        ''' </summary>
        Private Sub DetectFoldRegions(vRoot As SimpleSyntaxNode, vLineIndex As Integer, vMetadata As LineMetadata)
            Try
                ' Find nodes that span multiple lines starting at this line
                Dim lNodes = vRoot.DescendantNodes().Where(
                    Function(n)
                        Dim lSpan = n.GetLocation().GetLineSpan()
                        Return lSpan.StartLinePosition.Line = vLineIndex AndAlso
                               lSpan.EndLinePosition.Line > vLineIndex
                    End Function
                )
                
                for each lNode in lNodes
                    ' Check if this is a foldable node type
                    Select Case lNode.Kind()
                        Case SyntaxKind.ClassBlock, SyntaxKind.ModuleBlock,
                             SyntaxKind.InterfaceBlock, SyntaxKind.StructureBlock,
                             SyntaxKind.EnumBlock, SyntaxKind.NamespaceBlock,
                             SyntaxKind.SubBlock, SyntaxKind.FunctionBlock,
                             SyntaxKind.PropertyBlock, SyntaxKind.ConstructorBlock,
                             SyntaxKind.MultiLineIfBlock, SyntaxKind.WhileBlock,
                             SyntaxKind.ForBlock, SyntaxKind.DoLoopBlock,
                             SyntaxKind.SelectBlock, SyntaxKind.TryBlock,
                             SyntaxKind.WithBlock, SyntaxKind.UsingBlock
                            
                            ' Mark as foldable
                            vMetadata.IsFoldStart = True
                            Dim lEndLine = lNode.GetLocation().GetLineSpan().EndLinePosition.Line
                            vMetadata.FoldEndLine = lEndLine
                            Exit for  ' Only need one fold per line
                    End Select
                Next
                
                ' Check for #Region directives
                Dim lTrivia = vRoot.DescendantTrivia().Where(
                    Function(t)
                        Dim lSpan = t.GetLocation().GetLineSpan()
                        Return lSpan.StartLinePosition.Line = vLineIndex
                    End Function
                )
                
                for each lTriviaItem in lTrivia
                    If lTriviaItem.IsKind(SyntaxKind.RegionDirectiveTrivia) Then
                        vMetadata.IsFoldStart = True
                        vMetadata.IsRegion = True
                        ' Find matching #End Region
                        vMetadata.FoldEndLine = FindEndRegion(vRoot, vLineIndex)
                        Exit for
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DetectFoldRegions error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Finds the #End Region for a #Region directive
        ''' </summary>
        Private Function FindEndRegion(vRoot As SimpleSyntaxNode, vStartLine As Integer) As Integer
            Try
                Dim lEndRegions = vRoot.DescendantTrivia().Where(
                    Function(t) t.IsKind(SyntaxKind.EndRegionDirectiveTrivia)
                )
                
                for each lEndRegion in lEndRegions
                    Dim lEndLine = lEndRegion.GetLocation().GetLineSpan().StartLinePosition.Line
                    If lEndLine > vStartLine Then
                        Return lEndLine
                    End If
                Next
                
                Return vStartLine  ' No matching end found
                
            Catch ex As Exception
                Console.WriteLine($"FindEndRegion error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        ''' <summary>
        ''' Converts Roslyn tokens to SimpleIDE SyntaxTokens for a line
        ''' </summary>
        Private Function ConvertTokensForLine(vRoslynTokens As IEnumerable(Of Microsoft.CodeAnalysis.SyntaxToken), vLineStart As Integer) As List(Of Models.SyntaxToken)
            Try
                Dim lResult As New List(Of Models.SyntaxToken)
                
                for each lRoslynToken in vRoslynTokens
                    If lRoslynToken.Span.Length > 0 Then
                        Dim lToken As New Models.SyntaxToken()
                        lToken.StartColumn = lRoslynToken.SpanStart - vLineStart
                        lToken.EndColumn = lRoslynToken.Span.End - vLineStart
                        lToken.TokenType = pConverter.ConvertTokenType(lRoslynToken)
                        lToken.Text = lRoslynToken.Text
                        
                        ' Set styling
                        Select Case lRoslynToken.Kind()
                            Case SyntaxKind.ClassKeyword To SyntaxKind.YieldKeyword  ' All keywords
                                lToken.IsBold = True
                        End Select
                        
                        lResult.Add(lToken)
                    End If
                Next
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"ConvertTokensForLine error: {ex.Message}")
                Return New List(Of Models.SyntaxToken)
            End Try
        End Function
        
        ''' <summary>
        ''' Builds unified project tree from file trees
        ''' </summary>
        Private Function BuildUnifiedProjectTree(vFileTrees As Dictionary(Of String, FileSyntaxTree)) As SimpleSyntaxNode
            Try
                ' Create root node
                Dim lRootNode As New SyntaxNode(CodeNodeType.eProject, If(pProjectManager.CurrentProjectInfo?.AssemblyName, "Project"))
                
                ' Create namespace node
                Dim lNamespaceNode As New SyntaxNode(CodeNodeType.eNamespace, pRootNamespace)
                lNamespaceNode.IsImplicit = True
                lRootNode.AddChild(lNamespaceNode)
                
                ' Reset partial classes tracking
                pPartialClasses.Clear()
                
                ' Merge all file trees into unified structure
                for each lFileTree in vFileTrees.Values
                    If lFileTree.SimpleIDETree IsNot Nothing Then
                        MergeFileIntoProject(lNamespaceNode, lFileTree.SimpleIDETree)
                    End If
                Next
                
                Return lRootNode
                
            Catch ex As Exception
                Console.WriteLine($"BuildUnifiedProjectTree error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Merges a file's syntax tree into the project tree, handling partial classes
        ''' </summary>
        Private Sub MergeFileIntoProject(vNamespaceNode As SimpleSyntaxNode, vFileRoot As SimpleSyntaxNode)
            Try
                ' Process each child of the file root
                for each lChild in vFileRoot.Children
                    Select Case lChild.NodeType
                        Case CodeNodeType.eNamespace
                            ' Merge or create namespace
                            MergeNamespace(vNamespaceNode, lChild)
                            
                        Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eInterface
                            ' Check for partial class
                            Dim lKey = $"{vNamespaceNode.Name}.{lChild.Name}"
                            
                            If lChild.IsPartial AndAlso pPartialClasses.ContainsKey(lKey) Then
                                ' Merge into existing partial class
                                MergePartialType(pPartialClasses(lKey), lChild)
                            Else
                                ' Add as new type
                                vNamespaceNode.AddChild(lChild)
                                If lChild.IsPartial Then
                                    pPartialClasses(lKey) = lChild
                                End If
                            End If
                            
                        Case Else
                            ' Add other nodes directly
                            vNamespaceNode.AddChild(lChild)
                    End Select
                Next
                
            Catch ex As Exception
                Console.WriteLine($"MergeFileIntoProject error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Merges namespace nodes
        ''' </summary>
        Private Sub MergeNamespace(vTargetNamespace As SimpleSyntaxNode, vSourceNamespace As SimpleSyntaxNode)
            Try
                ' Find or create matching namespace
                Dim lExistingNamespace = vTargetNamespace.Children.FirstOrDefault(
                    Function(n) n.NodeType = CodeNodeType.eNamespace AndAlso n.Name = vSourceNamespace.Name
                )
                
                If lExistingNamespace IsNot Nothing Then
                    ' Merge into existing namespace recursively
                    for each lChild in vSourceNamespace.Children
                        MergeFileIntoProject(lExistingNamespace, vSourceNamespace)
                    Next
                Else
                    ' Add as new namespace
                    vTargetNamespace.AddChild(vSourceNamespace)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergeNamespace error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Merges partial type members
        ''' </summary>
        Private Sub MergePartialType(vExistingType As SimpleSyntaxNode, vNewType As SimpleSyntaxNode)
            Try
                Console.WriteLine($"Merging partial type {vNewType.Name}")
                
                ' Add all members from new partial to existing
                for each lMember in vNewType.Children
                    ' Check for duplicates
                    Dim lDuplicate = vExistingType.Children.Any(
                        Function(m) m.Name = lMember.Name AndAlso m.NodeType = lMember.NodeType
                    )
                    
                    If Not lDuplicate Then
                        vExistingType.AddChild(lMember)
                    End If
                Next
                
                ' Update file paths
                If vExistingType.Attributes Is Nothing Then
                    vExistingType.Attributes = New Dictionary(Of String, String)
                End If
                
                If vExistingType.Attributes.ContainsKey("FilePaths") Then
                    vExistingType.Attributes("FilePaths") &= ";" & vNewType.FilePath
                Else
                    vExistingType.Attributes("FilePaths") = vExistingType.FilePath & ";" & vNewType.FilePath
                End If
                
            Catch ex As Exception
                Console.WriteLine($"MergePartialType error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Calculates indent level for a line
        ''' </summary>
        Private Function CalculateIndentLevel(vLine As String) As Integer
            Dim lSpaces = 0
            for each lChar in vLine
                If lChar = " "c Then
                    lSpaces += 1
                ElseIf lChar = vbTab Then
                    lSpaces += 4
                Else
                    Exit for
                End If
            Next
            Return lSpaces \ 4
        End Function
        
        ''' <summary>
        ''' Gets the starting position of a line in the content
        ''' </summary>
        Private Function GetLineStartPosition(vContent As String, vLineIndex As Integer) As Integer
            If vLineIndex = 0 Then Return 0
            
            Dim lPosition = 0
            Dim lCurrentLine = 0
            
            for i = 0 To vContent.Length - 1
                If lCurrentLine = vLineIndex Then
                    Return i
                End If
                
                If vContent(i) = vbLf Then
                    lCurrentLine += 1
                ElseIf vContent(i) = vbCr Then
                    lCurrentLine += 1
                    If i + 1 < vContent.Length AndAlso vContent(i + 1) = vbLf Then
                        i += 1
                    End If
                End If
            Next
            
            Return lPosition
        End Function
        
    End Class
    
End Namespace