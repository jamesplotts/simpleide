' FileSyntaxTree.vb - Represents the parsed structure of a single VB.NET file
' Part of the Roslyn parser replacement
' Created: 2025-01-01

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.CodeAnalysis
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

' Resolve ambiguity
Imports SimpleSyntaxNode = SimpleIDE.Syntax.SyntaxNode

Namespace Parsers
    
    ''' <summary>
    ''' Represents the parsed structure of a single VB.NET file
    ''' </summary>
    Public Class FileSyntaxTree
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets or sets the file path
        ''' </summary>
        Public Property FilePath As String
        
        ''' <summary>
        ''' Gets or sets the Roslyn syntax tree
        ''' </summary>
        Public Property RoslynTree As Microsoft.CodeAnalysis.SyntaxTree
        
        ''' <summary>
        ''' Gets or sets the SimpleIDE syntax tree
        ''' </summary>
        Public Property SimpleIDETree As SimpleSyntaxNode
        
        ''' <summary>
        ''' Gets or sets the semantic model for type resolution
        ''' </summary>
        Public Property SemanticModel As SemanticModel
        
        ''' <summary>
        ''' Gets or sets the line metadata for syntax highlighting
        ''' </summary>
        Public Property LineMetadata As LineMetadata()
        
        ''' <summary>
        ''' Gets or sets parse diagnostics (errors/warnings)
        ''' </summary>
        Public Property Diagnostics As List(Of Diagnostic)
        
        ''' <summary>
        ''' Gets or sets whether the file has been successfully parsed
        ''' </summary>
        Public Property IsParsed As Boolean
        
        ''' <summary>
        ''' Gets or sets the last parse time
        ''' </summary>
        Public Property LastParseTime As DateTime
        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Initializes a new instance of FileSyntaxTree
        ''' </summary>
        Public Sub New()
            Diagnostics = New List(Of Diagnostic)()
            LastParseTime = DateTime.Now
            IsParsed = False
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Gets character tokens for a specific line
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        Public Function GetLineTokens(vLineIndex As Integer) As Byte()
            Try
                Dim lMetadata As LineMetadata() = LineMetadata
                If lMetadata IsNot Nothing AndAlso 
                   vLineIndex >= 0 AndAlso 
                   vLineIndex < lMetadata.Length Then
                    
                    If lMetadata(vLineIndex) IsNot Nothing Then
                        Return lMetadata(vLineIndex).CharacterColors
                    End If
                    Return New Byte() {}
                End If
                
                Return New Byte() {}
                
            Catch ex As Exception
                Console.WriteLine($"GetLineTokens error: {ex.Message}")
                Return New Byte() {}
            End Try
        End Function
        
        ''' <summary>
        ''' Gets syntax tokens for a specific line
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        Public Function GetLineSyntaxTokens(vLineIndex As Integer) As List(Of Models.SyntaxToken)
            Try
                Dim lMetadata As LineMetadata() = LineMetadata
                If lMetadata IsNot Nothing AndAlso 
                   vLineIndex >= 0 AndAlso 
                   vLineIndex < lMetadata.Length Then
                    
                    If lMetadata(vLineIndex) IsNot Nothing Then
                        Return lMetadata(vLineIndex).SyntaxTokens
                    End If
                    Return New List(Of Models.SyntaxToken)()
                End If
                
                Return New List(Of Models.SyntaxToken)()
                
            Catch ex As Exception
                Console.WriteLine($"GetLineSyntaxTokens error: {ex.Message}")
                Return New List(Of Models.SyntaxToken)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all errors in this file
        ''' </summary>
        Public Function GetErrors() As IEnumerable(Of Diagnostic)
            Try
                Return Diagnostics.Where(Function(d) d.Severity = DiagnosticSeverity.error)
                
            Catch ex As Exception
                Console.WriteLine($"GetErrors error: {ex.Message}")
                Return New List(Of Diagnostic)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all warnings in this file
        ''' </summary>
        Public Function GetWarnings() As IEnumerable(Of Diagnostic)
            Try
                Return Diagnostics.Where(Function(d) d.Severity = DiagnosticSeverity.Warning)
                
            Catch ex As Exception
                Console.WriteLine($"GetWarnings error: {ex.Message}")
                Return New List(Of Diagnostic)()
            End Try
        End Function
        
        ''' <summary>
        ''' Finds a node at the specified position
        ''' </summary>
        ''' <param name="vLine">Line number (0-based)</param>
        ''' <param name="vColumn">Column number (0-based)</param>
        Public Function FindNodeAt(vLine As Integer, vColumn As Integer) As SimpleSyntaxNode
            Try
                If SimpleIDETree Is Nothing Then Return Nothing
                
                Return FindNodeRecursive(SimpleIDETree, vLine, vColumn)
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeAt error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all nodes of a specific type
        ''' </summary>
        ''' <param name="vNodeType">The type of nodes to retrieve</param>
        Public Function GetNodesOfType(vNodeType As CodeNodeType) As List(Of SimpleSyntaxNode)
            Try
                Dim lResults As New List(Of SimpleSyntaxNode)()
                
                If SimpleIDETree IsNot Nothing Then
                    CollectNodesOfType(SimpleIDETree, vNodeType, lResults)
                End If
                
                Return lResults
                
            Catch ex As Exception
                Console.WriteLine($"GetNodesOfType error: {ex.Message}")
                Return New List(Of SimpleSyntaxNode)()
            End Try
        End Function
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Recursively finds a node at the specified position
        ''' </summary>
        Private Function FindNodeRecursive(vNode As SimpleSyntaxNode, vLine As Integer, vColumn As Integer) As SimpleSyntaxNode
            Try
                If vNode Is Nothing Then Return Nothing
                
                ' Check if this node contains the position
                If vLine >= vNode.StartLine AndAlso vLine <= vNode.EndLine Then
                    
                    ' Check children first (more specific)
                    for each lChild in vNode.Children
                        Dim lResult = FindNodeRecursive(lChild, vLine, vColumn)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                    
                    ' If no child matches, return this node
                    Return vNode
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeRecursive error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Recursively collects nodes of a specific type
        ''' </summary>
        Private Sub CollectNodesOfType(vNode As SimpleSyntaxNode, vNodeType As CodeNodeType, vResults As List(Of SimpleSyntaxNode))
            Try
                If vNode Is Nothing Then Return
                
                If vNode.NodeType = vNodeType Then
                    vResults.Add(vNode)
                End If
                
                for each lChild in vNode.Children
                    CollectNodesOfType(lChild, vNodeType, vResults)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CollectNodesOfType error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Updates line metadata from a Roslyn syntax tree
        ''' </summary>
        ''' <param name="vRoslynTree">The Roslyn syntax tree</param>
        ''' <param name="vLineCount">Number of lines in the file</param>
        Public Sub UpdateLineMetadata(vRoslynTree As Microsoft.CodeAnalysis.SyntaxTree, vLineCount As Integer)
            Try
                If vRoslynTree Is Nothing OrElse vLineCount <= 0 Then Return
                
                ' Initialize line metadata array
                ReDim LineMetadata(vLineCount - 1)
                
                for i = 0 To vLineCount - 1
                    LineMetadata(i) = New LineMetadata()
                    LineMetadata(i).LineNumber = i
                Next
                
                ' Process the tree to fill in metadata
                Dim lRoot = vRoslynTree.GetRoot()
                If lRoot IsNot Nothing Then
                    ProcessNodeForLineMetadata(lRoot, LineMetadata)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineMetadata error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Processes a Roslyn node to extract line metadata
        ''' </summary>
        Private Sub ProcessNodeForLineMetadata(vNode As Microsoft.CodeAnalysis.SyntaxNode, vLineMetadata As LineMetadata())
            Try
                If vNode Is Nothing OrElse vLineMetadata Is Nothing Then Return
                
                ' Get the span of this node
                Dim lSpan = vNode.GetLocation().GetLineSpan()
                Dim lStartLine = lSpan.StartLinePosition.Line
                Dim lEndLine = lSpan.EndLinePosition.Line
                
                ' Process tokens in this node
                for each lToken in vNode.DescendantTokens()
                    Dim lTokenSpan = lToken.GetLocation().GetLineSpan()
                    Dim lLine = lTokenSpan.StartLinePosition.Line
                    
                    If lLine >= 0 AndAlso lLine < vLineMetadata.Length Then
                        ' Add token info to the line metadata
                        ' This would normally add syntax token information
                        ' but simplified here for the fix
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessNodeForLineMetadata error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Clears all cached data
        ''' </summary>
        Public Sub Clear()
            Try
                RoslynTree = Nothing
                SimpleIDETree = Nothing
                SemanticModel = Nothing
                LineMetadata = Nothing
                Diagnostics.Clear()
                IsParsed = False
                
            Catch ex As Exception
                Console.WriteLine($"Clear error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace