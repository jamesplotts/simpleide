' Models/SourceFileInfo.vb - Source file representation with atomic text operations
' Created: 2025-01-10
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports SimpleIDE.Syntax
Imports SimpleIDE.Managers

Namespace Models
    
    ''' <summary>
    ''' Represents a source  file with text content, metadata, and syntax tokens
    ''' </summary>
    ''' <remarks>
    ''' This class manages the text content, line metadata, and character-level syntax tokens.
    ''' All text modifications go through the four atomic operations.
    ''' </remarks>
    Partial Public Class SourceFileInfo
        
        ' ===== Private Fields =====
        Private pFilePath As String = ""
        Private pTextLines As List(Of String)
        Private pLineMetadata() As LineMetadata
        Private pCharacterTokens()() As Byte
        Private pIsModified As Boolean = False
        Private pNeedsParsing As Boolean = False
        Private pIsLoaded As Boolean = False
        Private pLastModified As DateTime = DateTime.MinValue
        Private pLastParsed As DateTime = DateTime.MinValue
        Private pEncoding As Encoding = Encoding.UTF8
        Private pLanguage As String = "vb"
        Private pSyntaxTree As SyntaxNode
        Private pProjectManager As ProjectManager
        Private pParseErrors As List(Of ParseError)
        Private pKeywordCaseMap As Dictionary(Of String, String) = Nothing
        Private pIdentifierCaseMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)  
        Public Property ProjectRootNamespace As String = ""
        Public ProjectManager As ProjectManager

        ''' <summary>
        ''' Gets or sets the complete text content of the file
        ''' </summary>
        ''' <value>The entire file content as a single string</value>
        Public Property TextContent As String
            Get
                If pTextLines IsNot Nothing Then
                    Return String.Join(Environment.NewLine, pTextLines)
                End If
                Return ""
            End Get
            Set(value As String)
                If value IsNot Nothing Then
                    pTextLines = New List(Of String)(value.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None))
                    pIsModified = True
                    pNeedsParsing = True
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets the character colors array for each line
        ''' </summary>
        ''' <value>Jagged array where each element is a byte array for a line</value>
        Public Property CharacterColors As Byte()()
            Get
                Return pCharacterTokens
            End Get
            Set(value As Byte()())
                pCharacterTokens = value
            End Set
        End Property

        ' Demo Mode is used when you want to display a fictional file's content without having any file IO.
        Public IsDemoMode As Boolean = False
        Public IsVirtualFile As Boolean = False

        ''' <summary>
        ''' Timer handle for retrying ProjectManager acquisition
        ''' </summary>
        Private pRetryProjectManagerTimer As UInteger = 0
        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when text lines are changed
        ''' </summary>
        Public Event TextLinesChanged(sender As Object, e As TextLinesChangedEventArgs)
        
        ''' <summary>
        ''' Raised when rendering needs to be updated
        ''' </summary>
        Public Event RenderingChanged(sender As Object, e As EventArgs)
        
        ''' <summary>
        ''' Raised when the ProjectManager is needed for parsing
        ''' </summary>
        Public Event ProjectManagerRequested(sender As Object, e As ProjectManagerRequestEventArgs)

        ''' <summary>
        ''' Raised when the file content changes
        ''' </summary>
        Public Event ContentChanged As EventHandler

        
        ' ===== Constructor =====
        
        
        Public Sub New()
            MakeNew("", "")
        End Sub

        ''' <summary>
        ''' Initializes a new instance with a file path
        ''' </summary>
        ''' <param name="vFilePath">Path to the source file</param>
        Public Sub New(vFilePath As String)
            MakeNew(vFilePath, "")
        End Sub
        
        ''' <summary>
        ''' Initializes a new instance with file path and project directory
        ''' </summary>
        ''' <param name="vFilePath">Path to the source file</param>
        ''' <param name="vContent">Initial content (empty for files to be loaded)</param>
        ''' <param name="vProjectDirectory">Project directory path</param>
        Public Sub New(vFilePath As String, vContent As String)
            MakeNew(vFilePath, vContent)
        End Sub

        Public Sub MakeNew(vFilePath As String, vContent As String)
            Try
                pFilePath = vFilePath
                Dim lContent As String = vContent
    
                ' Special case: Empty filepath indicates virtual/temporary file
                If String.IsNullOrEmpty(vFilePath) Then
                    IsDemoMode = True
                    IsVirtualFile = True
                    vFilePath = "Untitled"  ' Give it a default name
                ' Check for virtual file indicators (AI artifacts, comparisons, etc.)
                ElseIf vFilePath.Contains("ai-artifact:") OrElse _
                       vFilePath.Contains("comparison:") OrElse _
                       vFilePath.Contains("Demo") OrElse _
                       vFilePath.Contains("Theme") OrElse _
                       vFilePath.Contains("Untitled") Then
                    IsVirtualFile = True
                    IsDemoMode = True
                ' Determine mode based on file existence and content
                ElseIf Not String.IsNullOrEmpty(vContent) Then
                    ' Content was explicitly provided
                    If Not File.Exists(vFilePath) Then
                        ' File doesn't exist but content provided - demo/virtual mode
                        IsDemoMode = True
                    End If
                ElseIf String.IsNullOrEmpty(vContent) AndAlso Not File.Exists(vFilePath) Then
                    ' No content and file doesn't exist - this is for a new file that will be created
                    IsDemoMode = False  ' Will be loaded/created later
                End If
    
                If IsDemoMode OrElse Not String.IsNullOrEmpty(lContent) Then
                    ' Demo/virtual mode or content explicitly provided - use the provided content
                    lContent = If(lContent, "")
                    'Console.WriteLine($"SourceFileInfo created with PROVIDED content for: {FileName} (Demo={lIsDemoMode}, Virtual={lIsVirtualFile})")
                End If
    
                If Not IsDemoMode AndAlso System.IO.Path.Exists(pFilePath) Then
                    lContent = System.IO.File.ReadAllText(pFilePath)
                    pTextLines = New List(Of String)(
                        lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                    )
                    If pTextLines.Count = 0 Then
                        pTextLines.Add("" + Environment.NewLine)
                        ReDim pLineMetadata(0)
                        pLineMetadata(0) = New LineMetadata()
                        ReDim pCharacterTokens(0)
                        pCharacterTokens(0) = New Byte() {}
                    
                    End If
                Else
                    ' If content provided, initialize with it
                    If Not String.IsNullOrEmpty(vContent) OrElse IsDemoMode Then
                        pTextLines = New List(Of String)(
                            vContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                        )
                        Dim lCount As Integer = pTextLines.Count
                        ReDim pLineMetadata(lCount)
                        ReDim pCharacterTokens(lCount)
                    Else
                        pTextLines.Add("" + Environment.Newline)
                        ReDim pLineMetadata(0)
                        pLineMetadata(0) = New LineMetadata()
                        ReDim pCharacterTokens(0)
                        pCharacterTokens(0) = New Byte() {}
                    End If
                End If
    
                ' Update text lines with case correction if not demo mode and not virtual
                If Not IsDemoMode AndAlso Not IsVirtualFile Then
                    for i As Integer = 0 To pTextLines.Count - 1
                        UpdateTextLineWithCaseCorrection(i)
                    Next
                End If
    
                ' Initialize collections
                ParseErrors = New List(Of ParseError)()
                InitializeLineMetadataArray()
                InitializeCharacterTokenArrays()
                pNeedsParsing = True
    
                ' Set state flags based on mode
                If IsDemoMode OrElse Not String.IsNullOrEmpty(vContent) Then
                    pIsLoaded = True  ' Content is immediately available
                Else
                    pIsLoaded = False  ' Regular files need LoadContent() to be called
                End If
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.MakeNew error: {ex.Message}")
                Console.WriteLine($"  Stack: {ex.StackTrace}")
                
                ' Ensure we have at least minimal valid state
                If pTextLines Is Nothing Then pTextLines = New List(Of String)({""})
                If pLineMetadata Is Nothing Then 
                    ReDim pLineMetadata(0)
                    pLineMetadata(0) = New LineMetadata()
                End If
            End Try
        End Sub

        Public Sub InitializeLineMetadataArray()
            ' Initialize LineMetadata array
            ReDim pLineMetadata(pTextLines.Count - 1)
            for i As Integer = 0 To pTextLines.Count - 1
                pLineMetadata(i) = New LineMetadata()
                pLineMetadata(i).UpdateHash(pTextLines(i))
                pLineMetadata(i).ParseState = LineParseState.eUnparsed
            Next
        End Sub

        Public Sub InitializeCharacterTokenArrays()
            ' Initialize CharacterTokens array with default tokens
            ReDim pCharacterTokens(pTextLines.Count - 1)
            for i As Integer = 0 To pTextLines.Count - 1
                Dim lLineLength As Integer = pTextLines(i).Length
                If lLineLength > 0 Then
                    pCharacterTokens(i) = pLineMetadata(i).GetEncodedTokens(lLineLength)
                Else
                    pCharacterTokens(i) = New Byte() {}
                End If
            Next
        End Sub
        
        ' ===== Public Properties =====

        ''' <summary>
        ''' Gets or sets the parse result from the parsing engine
        ''' </summary>
        Public Property ParseResult As SyntaxNode

        
        ''' <summary>
        ''' Gets or sets the file path
        ''' </summary>
        Public Property FilePath As String
            Get
                Return pFilePath
            End Get
            Set(value As String)
                pFilePath = If(value, "")
            End Set
        End Property
        
        ''' <summary>
        ''' Gets the file name without path
        ''' </summary>
        Public ReadOnly Property FileName As String
            Get
                If String.IsNullOrEmpty(pFilePath) Then Return "Untitled"
                Return System.IO.Path.GetFileName(pFilePath)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the text lines
        ''' </summary>
        Public ReadOnly Property TextLines As List(Of String)
            Get
                Return pTextLines
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the line metadata array
        ''' </summary>
        Public ReadOnly Property LineMetadata As LineMetadata()
            Get
                Return pLineMetadata
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the character tokens array
        ''' </summary>
        Public ReadOnly Property CharacterTokens As Byte()()
            Get
                Return pCharacterTokens
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets whether the file is modified
        ''' </summary>
        Public Property IsModified As Boolean
            Get
                Return pIsModified
            End Get
            Set(value As Boolean)
                pIsModified = value
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether the file needs parsing
        ''' </summary>
        Public Property NeedsParsing As Boolean
            Get
                Return pNeedsParsing
            End Get
            Set(value As Boolean)
                pNeedsParsing = value
            End Set
        End Property
        
        ''' <summary>
        ''' Gets the full text content
        ''' </summary>
        Public ReadOnly Property Content As String
            Get
                Return String.Join(Environment.NewLine, pTextLines)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the number of lines
        ''' </summary>
        Public ReadOnly Property LineCount As Integer
            Get
                Return If(pTextLines?.Count, 0)
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets the syntax tree for this file
        ''' </summary>
        Public Property SyntaxTree As SyntaxNode
            Get
                Return pSyntaxTree
            End Get
            Set(value As SyntaxNode)
                pSyntaxTree = value
            End Set
        End Property
        
        ''' <summary>
        ''' Gets the parse errors for this file
        ''' </summary>
        Public Property ParseErrors As List(Of ParseError)
            Get
                If pParseErrors Is Nothing Then
                    pParseErrors = New List(Of ParseError)
                End If
                Return pParseErrors
            End Get
            Set(value As List(Of ParseError))
                pParseErrors = value
                If pParseErrors Is Nothing Then pParseErrors = New List(Of ParseError)
            End Set
        End Property
        
        ''' <summary>
        ''' Gets whether the file has been parsed
        ''' </summary>
        Public ReadOnly Property IsParsed As Boolean
            Get
                Return pSyntaxTree IsNot Nothing
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the encoding used for this file
        ''' </summary>
        Public ReadOnly Property Encoding As Encoding
            Get
                Return pEncoding
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether the file is loaded
        ''' </summary>
        Public ReadOnly Property IsLoaded As Boolean
            Get
                Return pTextLines IsNot Nothing AndAlso pTextLines.Count > 0
            End Get
        End Property
        
        ''' <summary>
        ''' Gets or sets the last parsed date/time
        ''' </summary>
        Public Property LastParsed As DateTime
            Get
                Return pLastParsed
            End Get
            Set(value As DateTime)
                pLastParsed = value
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets the last modified date/time
        ''' </summary>
        Public Property LastModified As DateTime
            Get
                Return pLastModified
            End Get
            Set(value As DateTime)
                pLastModified = value
            End Set
        End Property
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Sets the project manager reference for this file
        ''' </summary>
        ''' <param name="vProjectManager">The project manager instance</param>
        Public Sub SetProjectManager(vProjectManager As ProjectManager)
            pProjectManager = vProjectManager
        End Sub
        
        ''' <summary>
        ''' Helper to raise text changed events
        ''' </summary>
        Private Sub RaiseTextChangedEvent(vType As TextChangeType, vStartLine As Integer, 
                                         vEndLine As Integer, vLinesAffected As Integer)
            Dim lArgs As New TextLinesChangedEventArgs() with {
                .ChangeType = vType,
                .StartLine = vStartLine,
                .EndLine = vEndLine,
                .LinesAffected = vLinesAffected,
                .NewLineCount = TextLines.Count
            }
            RaiseEvent TextLinesChanged(Me, lArgs)
            RaiseEvent RenderingChanged(Me, EventArgs.Empty)
        End Sub
        
        
        ' ===== Inner Classes =====
        
        ''' <summary>
        ''' Event arguments for text lines changed events
        ''' </summary>
        Public Class TextLinesChangedEventArgs
            Inherits EventArgs
            
            Public Property ChangeType As TextChangeType
            Public Property StartLine As Integer
            Public Property EndLine As Integer
            Public Property LinesAffected As Integer
            Public Property NewLineCount As Integer
        End Class
        
        ''' <summary>
        ''' Types of text changes
        ''' </summary>
        Public Enum TextChangeType
            eUnspecified
            eLineModified
            eLineInserted
            eLineDeleted
            eMultipleLines
            eCompleteReplace
            eLastValue
        End Enum
        
        ''' <summary>
        ''' Event arguments for ProjectManager request
        ''' </summary>
        Public Class ProjectManagerRequestEventArgs
            Inherits EventArgs
            
            Public Property ProjectManager As ProjectManager
            
            Public ReadOnly Property HasProjectManager As Boolean
                Get
                    Return ProjectManager IsNot Nothing
                End Get
            End Property
        End Class

        ' ===== PRIVATE HELPER METHODS =====
        

        Public Sub GenerateMetadata()
            for I As Integer = 0 To pTextLines.Count -1
                SetLineMetadataAndCharacterTokens(i)
            Next
        End Sub

        ''' <summary>
        ''' Cleans up timers and resources
        ''' </summary>
        ''' <remarks>
        ''' Should be called when disposing of the SourceFileInfo
        ''' </remarks>
        Public Sub Cleanup()
            Try
                ' Stop retry timer if running
                If pRetryProjectManagerTimer <> 0 Then
                    GLib.Source.Remove(pRetryProjectManagerTimer)
                    pRetryProjectManagerTimer = 0
                End If
                
                ' Clear event handlers if needed
                pProjectManager = Nothing
                
            Catch ex As Exception
                Console.WriteLine($"SourceFileInfo.Cleanup error: {ex.Message}")
            End Try
        End Sub

    End Class
    
End Namespace