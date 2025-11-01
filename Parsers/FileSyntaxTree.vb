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
        Public Property LineMetadata() As LineMetadata
        
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
                If LineMetadata IsNot Nothing AndAlso 
                   vLineIndex >= 0 AndAlso 
                   vLineIndex < LineMetadata.Length Then
                    
                    Return LineMetadata(vLineIndex).CharacterColors
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
                If LineMetadata IsNot Nothing AndAlso 
                   vLineIndex >= 0 AndAlso 
                   vLineIndex < LineMetadata.Length Then
                    
                    Return LineMetadata(vLineIndex).SyntaxTokens
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
        ''' Gets the node at a specific position
        ''' </summary>
        ''' <param name="vLine">Zero-based line number</param>
        ''' <param name="vColumn">Zero-based column number</param>
        Public Function GetNodeAtPosition(vLine As Integer, vColumn As Integer) As SimpleSyntaxNode
            Try
                If SimpleIDETree Is Nothing Then Return Nothing
                
                Return FindNodeAtPosition(SimpleIDETree, vLine, vColumn)
                
            Catch ex As Exception
                Console.WriteLine($"GetNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Gets all types defined in this file
        ''' </summary>
        Public Function GetTypes() As IEnumerable(Of SimpleSyntaxNode)
            Try
                Dim lTypes As New List(Of SimpleSyntaxNode)()
                
                If SimpleIDETree IsNot Nothing Then
                    CollectTypes(SimpleIDETree, lTypes)
                End If
                
                Return lTypes
                
            Catch ex As Exception
                Console.WriteLine($"GetTypes error: {ex.Message}")
                Return New List(Of SimpleSyntaxNode)()
            End Try
        End Function
        
        ''' <summary>
        ''' Gets XML documentation for a node at the specified line
        ''' </summary>
        ''' <param name="vLineIndex">Zero-based line index</param>
        Public Function GetXmlDocumentation(vLineIndex As Integer) As XmlDocInfo
            Try
                ' Find the node at this line
                Dim lNode = GetNodeAtLine(vLineIndex)
                If lNode IsNot Nothing Then
                    Return lNode.XmlDocumentation
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetXmlDocumentation error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if the file has any errors
        ''' </summary>
        Public Function HasErrors() As Boolean
            Return Diagnostics.Any(Function(d) d.Severity = DiagnosticSeverity.error)
        End Function
        
        ''' <summary>
        ''' Checks if the file has any warnings
        ''' </summary>
        Public Function HasWarnings() As Boolean
            Return Diagnostics.Any(Function(d) d.Severity = DiagnosticSeverity.Warning)
        End Function
        
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Recursively finds the node at a specific position
        ''' </summary>
        Private Function FindNodeAtPosition(vNode As SimpleSyntaxNode, vLine As Integer, vColumn As Integer) As SimpleSyntaxNode
            Try
                ' Check if position is within this node
                If vNode.ContainsPosition(vLine, vColumn) Then
                    ' Check children for more specific match
                    If vNode.Children IsNot Nothing Then
                        for each lChild in vNode.Children
                            Dim lResult = FindNodeAtPosition(lChild, vLine, vColumn)
                            If lResult IsNot Nothing Then
                                Return lResult
                            End If
                        Next
                    End If
                    
                    ' This is the most specific node containing the position
                    Return vNode
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the node that starts at a specific line
        ''' </summary>
        Private Function GetNodeAtLine(vLineIndex As Integer) As SimpleSyntaxNode
            Try
                If SimpleIDETree Is Nothing Then Return Nothing
                
                Return FindNodeAtLine(SimpleIDETree, vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"GetNodeAtLine error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Recursively finds a node that starts at a specific line
        ''' </summary>
        Private Function FindNodeAtLine(vNode As SimpleSyntaxNode, vLineIndex As Integer) As SimpleSyntaxNode
            Try
                ' Check if this node starts at the line
                If vNode.StartLine = vLineIndex Then
                    Return vNode
                End If
                
                ' Check children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        Dim lResult = FindNodeAtLine(lChild, vLineIndex)
                        If lResult IsNot Nothing Then
                            Return lResult
                        End If
                    Next
                End If
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindNodeAtLine error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Recursively collects all type nodes
        ''' </summary>
        Private Sub CollectTypes(vNode As SimpleSyntaxNode, vTypes As List(Of SimpleSyntaxNode))
            Try
                ' Check if this node is a type
                Select Case vNode.NodeType
                    Case CodeNodeType.eClass, CodeNodeType.eInterface,
                         CodeNodeType.eModule, CodeNodeType.eStructure,
                         CodeNodeType.eEnum
                        vTypes.Add(vNode)
                End Select
                
                ' Recurse through children
                If vNode.Children IsNot Nothing Then
                    for each lChild in vNode.Children
                        CollectTypes(lChild, vTypes)
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CollectTypes error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace