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
        

        ''' <summary>
        ''' Parse the content of the file using the centralized ProjectParser
        ''' </summary>
        ''' <returns>True if parsing succeeded, False otherwise</returns>
        ''' <remarks>
        ''' This method now delegates to the ProjectManager's parser when available.
        ''' If no ProjectManager is set, it returns False to avoid creating multiple parser instances.
        ''' The ProjectManager should call ParseFile on this SourceFileInfo instead.
        ''' </remarks>
        Public Function ParseContent() As Boolean
            Try
                ' Check if content is loaded
                If Not IsLoaded AndAlso Not IsDemoMode Then
                    Console.WriteLine($"Cannot parse {FileName}: content not loaded")
                    Return False
                End If
                
                ' NOTE: This method should NOT create its own parser anymore.
                ' The ProjectManager should be calling its ParseFile method instead,
                ' which will update this SourceFileInfo's SyntaxTree.
                ' For now, we'll just return False and log a message.
                
                Console.WriteLine($"SourceFileInfo.ParseContent: This method should not be called directly. Use ProjectManager.ParseFile instead.")
                Console.WriteLine($"  File: {FileName}")
                Console.WriteLine($"  Caller should use: ProjectManager.ParseFile(sourceFileInfo)")
                
                ' Return False to indicate parsing did not occur
                ' The calling code should be updated to use ProjectManager.ParseFile
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.ParseContent error: {ex.Message}")
                
                ' Add error to ParseErrors collection
                If ParseErrors Is Nothing Then
                    ParseErrors = New List(Of ParseError)()
                End If
                
                ParseErrors.Add(New ParseError with {
                    .Message = ex.Message,
                    .Line = 0,
                    .Column = 0,
                    .Severity = ParseErrorSeverity.eError
                })
                
                Return False
            End Try
        End Function
        
    End Class
    
End Namespace
