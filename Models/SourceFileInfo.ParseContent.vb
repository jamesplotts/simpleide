' Models/SourceFileInfo.ParseContent.vb - Enhanced parsing with project root namespace
Imports System
Imports System.IO
Imports SimpleIDE.Syntax

' SourceFileInfo.ParseContent.vb
' Created: 2025-08-10 13:36:13

Namespace Models
    
    Partial Public Class SourceFileInfo
        
        ' Store the project root namespace for parsing
        Private pProjectRootNamespace As String = "SimpleIDE"
        
'        ''' <summary>
'        ''' Set the project root namespace for parsing
'        ''' </summary>
'        Public Sub SetProjectRootNamespace(vRootNamespace As String)
'            If Not String.IsNullOrEmpty(vRootNamespace) Then
'                pProjectRootNamespace = vRootNamespace
'            End If
'        End Sub
        
'        ''' <summary>
'        ''' Parse the content of the file with the project's root namespace
'        ''' </summary>
'        Public Function ParseContent() As Boolean
'            Try
'                If String.IsNullOrEmpty(Content) Then
'                    Console.WriteLine($"No content to parse for {FileName}")
'                    Return False
'                End If
'                
'                ' Create parser with the project's root namespace
'                Dim lParser As New VBParser()
'                lParser.RootNamespace = pProjectRootNamespace
'                
'                Console.WriteLine($"Parsing {FileName} with root namespace: {pProjectRootNamespace}")
'                
'                ' Parse the content
'                Dim lResult As VBParser.ParseResult = lParser.Parse(Content, pProjectRootNamespace, FileName)
'                
'                If lResult IsNot Nothing Then
'                    SyntaxTree = lResult.RootNode
'                    ParseErrors = lResult.Errors
'                    LastParsed = DateTime.Now
'                    NeedsParsing = False
'                    
'                    ' Debug output
'                    If SyntaxTree IsNot Nothing Then
'                        Console.WriteLine($"  Parse result: {SyntaxTree.Children.Count} top-level nodes")
'                        For Each lChild In SyntaxTree.Children
'                            Console.WriteLine($"    - {lChild.Name} ({lChild.NodeType})")
'                            If lChild.NodeType = CodeNodeType.eNamespace Then
'                                Console.WriteLine($"      Has {lChild.Children.Count} children")
'                                For Each lSubChild In lChild.Children
'                                    Console.WriteLine($"        - {lSubChild.Name} ({lSubChild.NodeType})")
'                                Next
'                            End If
'                        Next
'                    End If
'                    
'                    Return ParseErrors.Count = 0 OrElse 
'                           Not ParseErrors.Any(Function(e) e.Severity = ParseErrorSeverity.eError)
'                End If
'                
'                Return False
'                
'            Catch ex As Exception
'                Console.WriteLine($"SourceFileInfo.ParseContent error: {ex.Message}")
'                ParseErrors = New List(Of ParseError) From {
'                    New ParseError With {
'                        .Message = $"Parse failed: {ex.Message}",
'                        .Line = 0,
'                        .Column = 0,
'                        .Severity = ParseErrorSeverity.eError
'                    }
'                }
'                Return False
'            End Try
'        End Function
        
    End Class
    
End Namespace
