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
        Public IsDemoMode As Boolean = False
        
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

        Public Sub New(vContent As String)
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
                    Return True
                End If
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
        ''' Parse the file (loads if needed)
        ''' </summary>
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
                If IsDemoMode Then Exit Sub
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

        ''' <summary>
        ''' Reload content from disk, discarding any in-memory changes
        ''' </summary>
        ''' <returns>True if successfully reloaded, False otherwise</returns>
        Public Function ReloadFromDisk() As Boolean
            Try
                If IsDemoMode Then 
                    Return True
                End If
                
                If Not File.Exists(FilePath) Then
                    Console.WriteLine($"ReloadFromDisk: File not found: {FilePath}")
                    Return False
                End If
                
                ' Read fresh content from disk
                Content = File.ReadAllText(FilePath)
                IsLoaded = True
                
                ' Split into lines
                TextLines = New List(Of String)(Content.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                If TextLines.Count = 0 Then
                    TextLines.Add("")
                End If
                
                ' Clear any existing parse data since content changed
                SyntaxTree = Nothing
                ParseErrors.Clear()
                IsParsed = False
                NeedsParsing = True
                
                ' Update editor if attached
                If Editor IsNot Nothing Then
                    ' Note: This will update the editor's internal copy of TextLines
                    ' but won't modify the displayed text unless explicitly refreshed
                    
                    ' WRONG - The editor has a pass-through property that returns the
                    ' reference to the TextLines existant in this instance. No update needed.
                End If
                
                Console.WriteLine($"ReloadFromDisk: Reloaded {Content.Length} characters from {FileName}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ReloadFromDisk error: {ex.Message}")
                Return False
            End Try
        End Function
        
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
        
    End Class
    
End Namespace
