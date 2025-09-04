' ParseCompletedEventArgs.vb
' Created: 2025-08-29 20:02:25

Imports System

Namespace Models

    ''' <summary>
    ''' Event arguments for parse completion notifications
    ''' </summary>
    Public Class ParseCompletedEventArgs
        Inherits EventArgs
        
        ''' <summary>
        ''' Gets the file path that was parsed
        ''' </summary>
        Public Property FilePath As String
        
        ''' <summary>
        ''' Gets the source file information
        ''' </summary>
        Public Property SourceFile As Models.SourceFileInfo
        
        ''' <summary>
        ''' Gets the parse result
        ''' </summary>
        Public Property ParseResult As DocumentParseResult
        
        ''' <summary>
        ''' Gets whether the parse was successful
        ''' </summary>
        Public Property Success As Boolean
        
        ''' <summary>
        ''' Gets any error message if parse failed
        ''' </summary>
        Public Property ErrorMessage As String
        
        ''' <summary>
        ''' Creates new parse completed event arguments
        ''' </summary>
        ''' <param name="vFilePath">The file that was parsed</param>
        ''' <param name="vSourceFile">The source file info</param>
        ''' <param name="vParseResult">The parse result</param>
        Public Sub New(vFilePath As String, vSourceFile As SourceFileInfo, vParseResult As DocumentParseResult)
            FilePath = vFilePath
            SourceFile = vSourceFile
            ParseResult = vParseResult
            Success = True
        End Sub
        
        ''' <summary>
        ''' Creates new parse completed event arguments for a failed parse
        ''' </summary>
        ''' <param name="vFilePath">The file that failed to parse</param>
        ''' <param name="vErrorMessage">The error message</param>
        Public Sub New(vFilePath As String, vErrorMessage As String)
            FilePath = vFilePath
            ErrorMessage = vErrorMessage
            Success = False
        End Sub
    End Class

End Namespace
