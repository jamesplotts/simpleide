' ParseResults.vb
' Created: 2025-08-31 23:19:26

Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models

Namespace Syntax

    ''' <summary>
    ''' Contains the results of parsing a source file
    ''' </summary>
    Public Class ParseResult
        
        ''' <summary>
        ''' Gets or sets the root syntax node of the parse tree
        ''' </summary>
        Public Property RootNode As SyntaxNode
        
        ''' <summary>
        ''' Gets or sets the line metadata array containing tokens for each line
        ''' </summary>
        Public Property LineMetadata As LineMetadata()
        
        ''' <summary>
        ''' Gets or sets any parse errors encountered
        ''' </summary>
        Public Property Errors As List(Of ParseError)
        
        ''' <summary>
        ''' Gets or sets the file path that was parsed
        ''' </summary>
        Public Property FilePath As String
        
        ''' <summary>
        ''' Gets or sets whether parsing was successful
        ''' </summary>
        Public Property Success As Boolean
        
        ''' <summary>
        ''' Gets or sets the time taken to parse
        ''' </summary>
        Public Property ParseTime As TimeSpan
        
        ''' <summary>
        ''' Gets or sets the list of class names found during parsing
        ''' </summary>
        Public Property ClassNames As List(Of String)
        
        ''' <summary>
        ''' Gets or sets the list of method names found during parsing
        ''' </summary>
        Public Property MethodNames As List(Of String)
        
        ''' <summary>
        ''' Gets or sets the list of property names found during parsing
        ''' </summary>
        Public Property PropertyNames As List(Of String)
        
        ''' <summary>
        ''' Creates a new ParseResult instance
        ''' </summary>
        Public Sub New()
            Errors = New List(Of ParseError)()
            ClassNames = New List(Of String)()
            MethodNames = New List(Of String)()
            PropertyNames = New List(Of String)()
            Success = True
        End Sub
        
    End Class

End Namespace