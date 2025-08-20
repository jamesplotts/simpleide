' Models/SourceFileInfo.vb - File parsing and structure information
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities
Imports SimpleIDE.Editors

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
        Public Property TextLines As List(Of String) = New List(Of String) From {""}
        Public Property Editor As CustomDrawingEditor
        Public Property RelativePath As String = ""  
        Public Property NeedsParsing As Boolean = True

        ' Demo Mode is used when you want to display a fictional file's content without having any file IO.
        Private IsDemoMode as Boolean = False
        
        ' ===== Constructor =====
        Public Sub New(vFilePath As String, vProjectDirectory As String)
            FilePath = vFilePath
            FileName = Path.GetFileName(vFilePath)
            ProjectDirectory = vProjectDirectory
            ParseErrors = New List(Of ParseError)()
            
            ' Calculate relative path
            Try
                Dim lFullPath As String = Path.GetFullPath(vFilePath)
                Dim lProjDir As String = Path.GetFullPath(vProjectDirectory)
                
                If lFullPath.StartsWith(lProjDir, StringComparison.OrdinalIgnoreCase) Then
                    RelativePath = lFullPath.Substring(lProjDir.Length)
                    If RelativePath.StartsWith(Path.DirectorySeparatorChar) Then
                        RelativePath = RelativePath.Substring(1)
                    End If
                Else
                    RelativePath = FileName
                End If
            Catch ex As Exception
                RelativePath = FileName
            End Try
        End Sub

        Public Sub New(vContent as String)
            IsDemoMode = True
            Content = vContent
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Load file content from disk
        ''' </summary>
        Public Function LoadContent() As Boolean
            Try
                If IsDemoMode Then 
                    return true
                End if
                If Not File.Exists(FilePath) Then
                    Console.WriteLine($"File not found: {FilePath}")
                    Return False
                End If
                
                Content = File.ReadAllText(FilePath)
                IsLoaded = True
                
                ' Split into lines
                TextLines = New List(Of String)(Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                If TextLines.Count = 0 Then
                    TextLines.Add("")
                End If
                
                Console.WriteLine($"Loaded {Content.Length} characters from {FileName}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.LoadContent error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Parse the loaded content
        ''' </summary>
        Public Function ParseContent() As Boolean
            Try
                If Not IsLoaded AndAlso Not IsDemoMode Then
                    Console.WriteLine($"Cannot parse {FileName}: content not loaded")
                    Return False
                End If
                
                ' Create parser - using VBParser instead of VBCodeParser
                Dim lParser As New VBParser()
                
                ' Parse the content using the Parse method with proper parameters
                Dim lParseResult As VBParser.ParseResult
                If Not IsDemoMode Then
                   lParseResult = lParser.Parse(Content, ProjectRootNamespace, FilePath)
                Else
                   lParseResult = lParser.Parse(Content, "", "")
                End If
                
                If lParseResult IsNot Nothing Then
                    ' The parse result already has a SyntaxNode tree
                    SyntaxTree = lParseResult.RootNode
                    
                    ' Store any parse errors
                    If lParseResult.Errors IsNot Nothing Then
                        ParseErrors = lParseResult.Errors
                    End If
                    
                    IsParsed = True
                    LastParsed = DateTime.Now
                    
                    Console.WriteLine($"Parsed {FileName}: {CountNodes(SyntaxTree)} nodes")
                    Return True
                Else
                    Console.WriteLine($"Parse failed for {FileName}")
                    Return False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ParseContent error: {ex.Message}")
                ParseErrors.Add(New ParseError With {
                    .Message = ex.Message,
                    .Line = 0,
                    .Column = 0
                })
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
                For Each lChild In vNode.Children
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
        ''' Parse the file (loads if needed)
        ''' </summary>
        Public Function ParseFile() As Boolean
            Try
                If IsDemoMode then Return ParseContent()
                ' Ensure content is loaded
                If Not IsLoaded Then
                    If Not LoadContent() Then
                        Console.WriteLine($"Failed to load content for {FileName}")
                        Return False
                    End If
                End If
                
                ' Parse the content
                Return ParseContent()
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ParseFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Load and parse the file in one operation
        ''' </summary>
        Public Function LoadAndParse() As Boolean
            Try
                If Not LoadContent() Then Return False
                Return ParseContent()
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.LoadAndParse error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Update content from editor
        ''' </summary>
        Public Sub UpdateFromEditor()
            Try
                'If IsDemoMode Then Exit Sub
                If Editor IsNot Nothing Then
                    Content = Editor.GetAllText()
                    TextLines = New List(Of String)(Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                    If TextLines.Count = 0 Then
                        TextLines.Add("")
                    End If
                    IsLoaded = True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.UpdateFromEditor error: {ex.Message}")
            End Try
        End Sub
        
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
                        For Each lChild In vNode.Children
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
                If IsDemoMode then Exit Sub
                If SyntaxTree Is Nothing OrElse vRootNamespace Is Nothing Then
                    Console.WriteLine($"Cannot merge {FileName}: no syntax tree or root namespace")
                    Return
                End If
                
                ' If the file has top-level nodes, merge them
                For Each lNode In SyntaxTree.Children
                    If lNode.NodeType = CodeNodeType.eNamespace AndAlso 
                       lNode.Name = vRootNamespace.Name AndAlso 
                       lNode.IsImplicit Then
                        ' This is the implicit root namespace - merge its children directly
                        For Each lChild In lNode.Children
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
            If IsDemoMode then Exit Sub
            If Not String.IsNullOrEmpty(vRootNamespace) Then
                ProjectRootNamespace = vRootNamespace
            End If
        End Sub
        
        ' Helper method to merge a node into the project tree
        Private Sub MergeNodeIntoProject(vNode As SyntaxNode, vParentNode As SyntaxNode)
            Try
                If IsDemoMode then Exit Sub
                If vNode Is Nothing OrElse vParentNode Is Nothing Then Return
                
                ' Check if a similar node already exists
                Dim lExistingNode As SyntaxNode = Nothing
                For Each lChild In vParentNode.Children
                    If lChild.Name = vNode.Name AndAlso lChild.NodeType = vNode.NodeType Then
                        lExistingNode = lChild
                        Exit For
                    End If
                Next
                
                If lExistingNode IsNot Nothing Then
                    ' Merge children into existing node
                    For Each lChild In vNode.Children
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
                For Each lChild In vNode.Children
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
                If IsDemoMode then Return True
                If String.IsNullOrEmpty(FilePath) Then
                    Console.WriteLine($"Cannot save {FileName}: no file path")
                    Return False
                End If
                
                ' Update content from editor if attached
                If Editor IsNot Nothing Then
                    UpdateFromEditor()
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
                If IsDemoMode then Exit Sub
                Using lReader As New StreamReader(vStream, vEncoding)
                    Content = lReader.ReadToEnd()
                    TextLines = New List(Of String)(Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                    If TextLines.Count = 0 Then
                        TextLines.Add("")
                    End If
                    IsLoaded = True
                    NeedsParsing = True
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.LoadFromStream error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
