' DocumentModel.vb
' Created: 2025-08-07 17:55:16
' Models/DocumentModel.vb - Complete in-memory document model with rich metadata
Imports System
Imports System.IO
Imports System.Text
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Namespace Models
    
    ''' <summary>
    ''' Document model that keeps ALL text in memory at ALL times
    ''' Created at project load time and maintains complete text and structure
    ''' </summary>
    Public Class DocumentModel
        
        ' ===== Enums =====
        
        ''' <summary>
        ''' Parse state for individual lines
        ''' </summary>
        Public Enum LineParseState
            eUnspecified
            eUnparsed
            eParsed
            eBeingEdited
            eParseError
            eLastValue
        End Enum
        
        ''' <summary>
        ''' Rendering mode for lines
        ''' </summary>
        Public Enum LineRenderMode
            eUnspecified
            eNormal
            eHighlighted
            eError
            eWarning
            eBeingEdited
            eBreakpoint
            eCurrentExecutionLine
            eLastValue
        End Enum
        
        ' ===== Private Fields =====
        
        Private pFilePath As String = ""
        Private pDocumentLines As List(Of DocumentLine)
        Private pRootNode As DocumentNode
        Private pNodeLookup As Dictionary(Of String, DocumentNode)
        Private pSymbolIndex As Dictionary(Of String, List(Of DocumentNode))
        Private pMethodBoundaryCache As Dictionary(Of Integer, DocumentNode)
        Private pScopeCache As Dictionary(Of Integer, ScopeInfo)
        Private pAttachedEditors As List(Of IEditor)
        Private pIsModified As Boolean = False
        Private pDocumentVersion As Integer = 0
        Private pEncoding As Encoding = Encoding.UTF8
        'Private pParser As VBCodeParser
        Private pDeferredParseLineIndex As Integer = -1
        Private pParseTimer As UInteger = 0
        Private pTokenCache As Dictionary(Of Integer, List(Of SyntaxToken))
        Private pDeclarationRegistry As Dictionary(Of String, DeclarationInfo)
        Private pCrossReferences As Dictionary(Of String, List(Of ReferenceInfo))


        ''' <summary>
        ''' Cached reference to ProjectManager obtained via event
        ''' </summary>
        Private pCachedProjectManager As Object

        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when document has been parsed
        ''' </summary>
        Public Event DocumentParsed(vRootNode As DocumentNode)
        
        ''' <summary>
        ''' Raised when a line's rendering mode changes
        ''' </summary>
        Public Event LineRenderingChanged(vLineIndex As Integer, vRenderMode As LineRenderMode)
        
        ''' <summary>
        ''' Raised when document structure changes
        ''' </summary>
        Public Event StructureChanged(vAffectedNodes As List(Of DocumentNode))
        
        ''' <summary>
        ''' Raised when text content changes
        ''' </summary>
        Public Event TextChanged(vStartLine As Integer, vEndLine As Integer)
        
        ''' <summary>
        ''' Raised when modified state changes
        ''' </summary>
        Public Event ModifiedStateChanged(vIsModified As Boolean)

        ''' <summary>
        ''' Event to request ProjectManager reference
        ''' </summary>
        Public Event RequestProjectManager As EventHandler(Of ProjectManagerRequestEventArgs)

        
        ' ===== Constructor =====
        
        ''' <summary>
        ''' Creates a new DocumentModel instance without local parser
        ''' </summary>
        ''' <param name="vFilePath">Optional file path for the document</param>
        Public Sub New(Optional vFilePath As String = "")
            Try
                pFilePath = vFilePath
                pDocumentLines = New List(Of DocumentLine)()
                pNodeLookup = New Dictionary(Of String, DocumentNode)()
                pSymbolIndex = New Dictionary(Of String, List(Of DocumentNode))(StringComparer.OrdinalIgnoreCase)
                pMethodBoundaryCache = New Dictionary(Of Integer, DocumentNode)()
                pScopeCache = New Dictionary(Of Integer, ScopeInfo)()
                pAttachedEditors = New List(Of IEditor)()
                ' NOTE: Removed pParser = New VBCodeParser() - now using centralized parser
                pTokenCache = New Dictionary(Of Integer, List(Of SyntaxToken))()
                pDeclarationRegistry = New Dictionary(Of String, DeclarationInfo)(StringComparer.OrdinalIgnoreCase)
                pCrossReferences = New Dictionary(Of String, List(Of ReferenceInfo))(StringComparer.OrdinalIgnoreCase)
                
                ' Initialize with one empty line
                pDocumentLines.Add(New DocumentLine())
                
                ' Subscribe to ProjectManager parse events if available
                SubscribeToProjectManagerEvents()
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Properties =====
        
        Public ReadOnly Property FilePath As String
            Get
                Return pFilePath
            End Get
        End Property
        
        Public ReadOnly Property IsModified As Boolean
            Get
                Return pIsModified
            End Get
        End Property
        
        Public ReadOnly Property LineCount As Integer
            Get
                Return pDocumentLines.Count
            End Get
        End Property
        
        Public ReadOnly Property RootNode As DocumentNode
            Get
                Return pRootNode
            End Get
        End Property
        
        Public ReadOnly Property DocumentVersion As Integer
            Get
                Return pDocumentVersion
            End Get
        End Property
        
        Public ReadOnly Property AllDeclarations As IEnumerable(Of DeclarationInfo)
            Get
                Return pDeclarationRegistry.Values
            End Get
        End Property
        
        ' ===== File Operations =====
        
        ''' <summary>
        ''' Load file content at project load time - keeps everything in memory
        ''' </summary>
        Public Function LoadFromFile(vFilePath As String) As Boolean
            Try
                pFilePath = vFilePath
                
                If Not File.Exists(vFilePath) Then
                    Console.WriteLine($"DocumentModel.LoadFromFile: File not found: {vFilePath}")
                    Return False
                End If
                
                ' Read all lines into memory
                Dim lLines As String() = File.ReadAllLines(vFilePath, pEncoding)
                
                ' Clear existing content
                pDocumentLines.Clear()
                pNodeLookup.Clear()
                pSymbolIndex.Clear()
                pMethodBoundaryCache.Clear()
                pScopeCache.Clear()
                pTokenCache.Clear()
                pDeclarationRegistry.Clear()
                pCrossReferences.Clear()
                pRootNode = Nothing
                
                ' Create DocumentLine objects for each line
                For Each lLine In lLines
                    Dim lDocLine As New DocumentLine()
                    lDocLine.Text = lLine
                    lDocLine.ParseState = LineParseState.eUnparsed
                    lDocLine.LineNumber = pDocumentLines.Count
                    pDocumentLines.Add(lDocLine)
                Next
                
                ' Ensure at least one line exists
                If pDocumentLines.Count = 0 Then
                    pDocumentLines.Add(New DocumentLine())
                End If
                
                ' Perform initial full parse
                ParseDocument()
                
                ' Pre-tokenize all lines for fast syntax highlighting
                PreTokenizeAllLines()
                
                ' Build indices for fast lookups
                BuildIndices()
                
                ' Mark as unmodified
                SetModified(False)
                pDocumentVersion = 1
                
                Console.WriteLine($"DocumentModel.LoadFromFile: loaded {pDocumentLines.Count} lines from {vFilePath}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.LoadFromFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Save document to file
        ''' </summary>
        Public Function SaveToFile(Optional vFilePath As String = "") As Boolean
            Try
                If String.IsNullOrEmpty(vFilePath) Then
                    vFilePath = pFilePath
                End If
                
                If String.IsNullOrEmpty(vFilePath) Then
                    Console.WriteLine("DocumentModel.SaveToFile: No file Path specified")
                    Return False
                End If
                
                ' Build complete text from lines
                Dim lText As String = GetAllText()
                
                ' Write to file
                File.WriteAllText(vFilePath, lText, pEncoding)
                
                ' Update path and modified state
                pFilePath = vFilePath
                SetModified(False)
                
                Console.WriteLine($"DocumentModel.SaveToFile: Saved to {vFilePath}")
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.SaveToFile error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Editor Management =====
        
        ''' <summary>
        ''' Attach an editor instance to this document
        ''' </summary>
        Public Sub AttachEditor(vEditor As IEditor)
            Try
                If vEditor Is Nothing Then Return
                
                If Not pAttachedEditors.Contains(vEditor) Then
                    pAttachedEditors.Add(vEditor)
                End If
                
                Console.WriteLine($"DocumentModel.AttachEditor: Editor attached, total editors: {pAttachedEditors.Count}")
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AttachEditor error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Detach an editor instance from this document
        ''' </summary>
        Public Sub DetachEditor(vEditor As IEditor)
            Try
                If vEditor Is Nothing Then Return
                
                pAttachedEditors.Remove(vEditor)
                
                Console.WriteLine($"DocumentModel.DetachEditor: Editor detached, remaining editors: {pAttachedEditors.Count}")
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.DetachEditor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Line Editing =====
        
        ''' <summary>
        ''' Begin editing on a line, deferring parsing until editing completes
        ''' </summary>
        Public Sub BeginLineEdit(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then Return
                
                Dim lLine As DocumentLine = pDocumentLines(vLineIndex)
                lLine.IsBeingEdited = True
                lLine.ParseState = LineParseState.eBeingEdited
                
                ' CRITICAL FIX: Clear any existing parse timer with proper cleanup
                If pParseTimer <> 0 Then
                    Dim lTimerId As UInteger = pParseTimer
                    pParseTimer = 0  ' Clear BEFORE removing
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
                
                ' Store the line to parse when editing ends
                pDeferredParseLineIndex = vLineIndex
                
                ' Notify rendering change
                RaiseEvent LineRenderingChanged(vLineIndex, LineRenderMode.eBeingEdited)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.BeginLineEdit error: {ex.Message}")
            End Try
        End Sub

        
        ''' <summary>
        ''' End editing on a line and trigger deferred parsing
        ''' </summary>
        Public Sub EndLineEdit(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then Return
                
                Dim lLine As DocumentLine = pDocumentLines(vLineIndex)
                lLine.IsBeingEdited = False
                
                ' CRITICAL FIX: Schedule parsing after a short delay with proper cleanup
                If pParseTimer <> 0 Then
                    Dim lTimerId As UInteger = pParseTimer
                    pParseTimer = 0  ' Clear BEFORE removing
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
                
                pParseTimer = GLib.Timeout.Add(300, Function()
                    ' Clear timer ID immediately since we're returning False
                    pParseTimer = 0
                    ParseLine(vLineIndex)
                    UpdateIncrementalStructure(vLineIndex)
                    Return False  ' Timer is auto-removed
                End Function)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.EndLineEdit error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Text Modification Operations =====
        
        ''' <summary>
        ''' Update an entire line
        ''' </summary>
        Public Sub UpdateLine(vLineIndex As Integer, vNewText As String)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then Return
                
                Dim lLine As DocumentLine = pDocumentLines(vLineIndex)
                Dim lOldText As String = lLine.Text
                
                ' Update text
                lLine.Text = If(vNewText, "")
                lLine.MarkChanged()
                
                ' Clear cached tokens for this line
                If pTokenCache.ContainsKey(vLineIndex) Then
                    pTokenCache.Remove(vLineIndex)
                End If
                
                ' Mark as modified
                SetModified(True)
                pDocumentVersion += 1
                
                ' Raise text changed event
                RaiseEvent TextChanged(vLineIndex, vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.UpdateLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Insert text at specified position
        ''' </summary>
        Public Sub InsertText(vLineIndex As Integer, vColumn As Integer, vText As String)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then Return
                If String.IsNullOrEmpty(vText) Then Return
                
                Dim lLine As DocumentLine = pDocumentLines(vLineIndex)
                Dim lCurrentText As String = lLine.Text
                
                ' Ensure column is valid
                If vColumn < 0 Then vColumn = 0
                If vColumn > lCurrentText.Length Then vColumn = lCurrentText.Length
                
                ' Check for newline in inserted text
                If vText.Contains(Environment.NewLine) OrElse vText.Contains(vbLf) Then
                    ' Handle multi-line insert
                    InsertMultilineText(vLineIndex, vColumn, vText)
                Else
                    ' Simple single-line insert
                    lLine.Text = lCurrentText.Substring(0, vColumn) & vText & lCurrentText.Substring(vColumn)
                    lLine.MarkChanged()
                    
                    ' Clear cached tokens
                    If pTokenCache.ContainsKey(vLineIndex) Then
                        pTokenCache.Remove(vLineIndex)
                    End If
                    
                    ' Adjust node coordinates
                    AdjustNodeCoordinatesAfterInsert(vLineIndex, vColumn, vText.Length)
                End If
                
                ' Mark as modified
                SetModified(True)
                pDocumentVersion += 1
                
                ' Raise text changed event
                RaiseEvent TextChanged(vLineIndex, vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.InsertText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Delete text at specified position
        ''' </summary>
        Public Sub DeleteText(vLineIndex As Integer, vStartColumn As Integer, vLength As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then Return
                If vLength <= 0 Then Return
                
                Dim lLine As DocumentLine = pDocumentLines(vLineIndex)
                Dim lCurrentText As String = lLine.Text
                
                ' Validate columns
                If vStartColumn < 0 Then vStartColumn = 0
                If vStartColumn >= lCurrentText.Length Then Return
                
                Dim lEndColumn As Integer = Math.Min(vStartColumn + vLength, lCurrentText.Length)
                
                ' Delete the text
                lLine.Text = lCurrentText.Substring(0, vStartColumn) & lCurrentText.Substring(lEndColumn)
                lLine.MarkChanged()
                
                ' Clear cached tokens
                If pTokenCache.ContainsKey(vLineIndex) Then
                    pTokenCache.Remove(vLineIndex)
                End If
                
                ' Adjust node coordinates
                AdjustNodeCoordinatesAfterDelete(vLineIndex, vStartColumn, vLength)
                
                ' Mark as modified
                SetModified(True)
                pDocumentVersion += 1
                
                ' Raise text changed event
                RaiseEvent TextChanged(vLineIndex, vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.DeleteText error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Insert a new line
        ''' </summary>
        Public Sub InsertLine(vLineIndex As Integer, vText As String)
            Try
                ' Validate index
                If vLineIndex < 0 Then vLineIndex = 0
                If vLineIndex > pDocumentLines.Count Then vLineIndex = pDocumentLines.Count
                
                ' Create new line
                Dim lNewLine As New DocumentLine()
                lNewLine.Text = If(vText, "")
                lNewLine.ParseState = LineParseState.eUnparsed
                lNewLine.LineNumber = vLineIndex
                
                ' Insert line
                pDocumentLines.Insert(vLineIndex, lNewLine)
                
                ' Update line numbers for subsequent lines
                For i As Integer = vLineIndex + 1 To pDocumentLines.Count - 1
                    pDocumentLines(i).LineNumber = i
                Next
                
                ' Adjust node coordinates
                AdjustNodeCoordinatesAfterLineInsert(vLineIndex)
                
                ' Clear method boundary cache
                pMethodBoundaryCache.Clear()
                pScopeCache.Clear()
                
                ' Mark as modified
                SetModified(True)
                pDocumentVersion += 1
                
                ' Raise text changed event
                RaiseEvent TextChanged(vLineIndex, pDocumentLines.Count - 1)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.InsertLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Delete a line
        ''' </summary>
        Public Sub DeleteLine(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then Return
                
                ' Don't delete if it's the only line
                If pDocumentLines.Count = 1 Then
                    ' Just clear the line
                    pDocumentLines(0).Text = ""
                    pDocumentLines(0).MarkChanged()
                Else
                    ' Remove the line
                    pDocumentLines.RemoveAt(vLineIndex)
                    
                    ' Update line numbers for subsequent lines
                    For i As Integer = vLineIndex To pDocumentLines.Count - 1
                        pDocumentLines(i).LineNumber = i
                    Next
                    
                    ' Clear cached tokens
                    If pTokenCache.ContainsKey(vLineIndex) Then
                        pTokenCache.Remove(vLineIndex)
                    End If
                    
                    ' Adjust node coordinates
                    AdjustNodeCoordinatesAfterLineDelete(vLineIndex)
                End If
                
                ' Clear method boundary cache
                pMethodBoundaryCache.Clear()
                pScopeCache.Clear()
                
                ' Mark as modified
                SetModified(True)
                pDocumentVersion += 1
                
                ' Raise text changed event
                RaiseEvent TextChanged(vLineIndex, pDocumentLines.Count - 1)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.DeleteLine error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Text Retrieval =====
        
        ''' <summary>
        ''' Get the full document text
        ''' </summary>
        Public Function GetAllText() As String
            Try
                Dim lBuilder As New StringBuilder()
                
                For i As Integer = 0 To pDocumentLines.Count - 1
                    lBuilder.Append(pDocumentLines(i).Text)
                    If i < pDocumentLines.Count - 1 Then
                        lBuilder.AppendLine()
                    End If
                Next
                
                Return lBuilder.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetAllText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Get text for a range of lines
        ''' </summary>
        Public Function GetTextRange(vStartLine As Integer, vEndLine As Integer) As String
            Try
                If vStartLine < 0 Then vStartLine = 0
                If vEndLine >= pDocumentLines.Count Then vEndLine = pDocumentLines.Count - 1
                If vStartLine > vEndLine Then Return ""
                
                Dim lBuilder As New StringBuilder()
                
                For i As Integer = vStartLine To vEndLine
                    lBuilder.Append(pDocumentLines(i).Text)
                    If i < vEndLine Then
                        lBuilder.AppendLine()
                    End If
                Next
                
                Return lBuilder.ToString()
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetTextRange error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Get a specific line
        ''' </summary>
        Public Function GetLine(vIndex As Integer) As DocumentLine
            Try
                If vIndex >= 0 AndAlso vIndex < pDocumentLines.Count Then
                    Return pDocumentLines(vIndex)
                End If
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetLine error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get line text
        ''' </summary>
        Public Function GetLineText(vIndex As Integer) As String
            Try
                Dim lLine As DocumentLine = GetLine(vIndex)
                If lLine IsNot Nothing Then
                    Return lLine.Text
                End If
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetLineText error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ' ===== Query Methods =====
        
        ''' <summary>
        ''' Get the node at a specific position
        ''' </summary>
        Public Function GetNodeAtPosition(vLine As Integer, vColumn As Integer) As DocumentNode
            Try
                If pRootNode Is Nothing Then Return Nothing
                
                Return FindNodeAtPosition(pRootNode, vLine, vColumn)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get the containing scope at a position
        ''' </summary>
        Public Function GetContainingScope(vLine As Integer) As ScopeInfo
            Try
                ' Check cache first
                If pScopeCache.ContainsKey(vLine) Then
                    Return pScopeCache(vLine)
                End If
                
                ' Find containing node
                Dim lNode As DocumentNode = GetNodeAtPosition(vLine, 0)
                If lNode Is Nothing Then Return Nothing
                
                ' Build scope info
                Dim lScope As New ScopeInfo()
                lScope.Line = vLine
                lScope.ContainingNode = lNode
                
                ' Find containing class/module
                Dim lCurrent As DocumentNode = lNode
                While lCurrent IsNot Nothing
                    Select Case lCurrent.NodeType
                        Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eStructure
                            lScope.ContainingClass = lCurrent
                            Exit While
                    End Select
                    lCurrent = lCurrent.Parent
                End While
                
                ' Find containing method
                lCurrent = lNode
                While lCurrent IsNot Nothing
                    Select Case lCurrent.NodeType
                        Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor
                            lScope.ContainingMethod = lCurrent
                            Exit While
                    End Select
                    lCurrent = lCurrent.Parent
                End While
                
                ' Collect variables in scope
                CollectVariablesInScope(lScope, vLine)
                
                ' Cache the result
                pScopeCache(vLine) = lScope
                
                Return lScope
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetContainingScope error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Get all declarations in the document
        ''' </summary>
        Public Function GetAllDeclarations() As List(Of DeclarationInfo)
            Try
                Return pDeclarationRegistry.Values.ToList()
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetAllDeclarations error: {ex.Message}")
                Return New List(Of DeclarationInfo)()
            End Try
        End Function
        
        ''' <summary>
        ''' Find a symbol by name
        ''' </summary>
        Public Function FindSymbol(vName As String) As List(Of DocumentNode)
            Try
                If String.IsNullOrEmpty(vName) Then Return New List(Of DocumentNode)()
                
                ' Check symbol index
                If pSymbolIndex.ContainsKey(vName) Then
                    Return New List(Of DocumentNode)(pSymbolIndex(vName))
                End If
                
                Return New List(Of DocumentNode)()
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.FindSymbol error: {ex.Message}")
                Return New List(Of DocumentNode)()
            End Try
        End Function
        
        ''' <summary>
        ''' Get tokens for a line (from cache or parse)
        ''' </summary>
        Public Function GetLineTokens(vLineIndex As Integer) As List(Of SyntaxToken)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then
                    Return New List(Of SyntaxToken)()
                End If
                
                ' Check cache first
                If pTokenCache.ContainsKey(vLineIndex) Then
                    Return pTokenCache(vLineIndex)
                End If
                
                ' Parse and cache tokens
                Dim lTokens As List(Of SyntaxToken) = ParseLineTokens(vLineIndex)
                pTokenCache(vLineIndex) = lTokens
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.GetLineTokens error: {ex.Message}")
                Return New List(Of SyntaxToken)()
            End Try
        End Function
        
        ' ===== Performance Optimization Methods =====
        
        ''' <summary>
        ''' Pre-tokenize all lines for fast syntax highlighting
        ''' </summary>
        Private Sub PreTokenizeAllLines()
            Try
                pTokenCache.Clear()
                
                For i As Integer = 0 To pDocumentLines.Count - 1
                    Dim lTokens As List(Of SyntaxToken) = ParseLineTokens(i)
                    pTokenCache(i) = lTokens
                    pDocumentLines(i).Tokens = lTokens
                Next
                
                Console.WriteLine($"DocumentModel.PreTokenizeAllLines: Tokenized {pDocumentLines.Count} lines")
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.PreTokenizeAllLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Build indices for fast symbol lookup
        ''' </summary>
        Private Sub BuildIndices()
            Try
                pSymbolIndex.Clear()
                pDeclarationRegistry.Clear()
                pMethodBoundaryCache.Clear()
                
                If pRootNode Is Nothing Then Return
                
                ' Build symbol index and declaration registry
                BuildIndicesRecursive(pRootNode)
                
                ' Build method boundary cache
                For i As Integer = 0 To pDocumentLines.Count - 1
                    Dim lMethod As DocumentNode = FindContainingMethod(i)
                    If lMethod IsNot Nothing Then
                        pMethodBoundaryCache(i) = lMethod
                    End If
                Next
                
                Console.WriteLine($"DocumentModel.BuildIndices: Indexed {pSymbolIndex.Count} symbols, {pDeclarationRegistry.Count} declarations")
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.BuildIndices error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub BuildIndicesRecursive(vNode As DocumentNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Add to symbol index
                If Not String.IsNullOrEmpty(vNode.Name) Then
                    If Not pSymbolIndex.ContainsKey(vNode.Name) Then
                        pSymbolIndex(vNode.Name) = New List(Of DocumentNode)()
                    End If
                    pSymbolIndex(vNode.Name).Add(vNode)
                End If
                
                ' Add to declaration registry if it's a declaration
                If IsDeclarationNode(vNode.NodeType) Then
                    Dim lDecl As New DeclarationInfo()
                    lDecl.Node = vNode
                    lDecl.Name = vNode.Name
                    lDecl.FullName = vNode.FullName
                    lDecl.DeclarationType = vNode.NodeType
                    lDecl.Line = vNode.StartLine
                    lDecl.Column = vNode.StartColumn
                    
                    pDeclarationRegistry(lDecl.FullName) = lDecl
                End If
                
                ' Process children
                For Each lChild In vNode.Children
                    BuildIndicesRecursive(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.BuildIndicesRecursive error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Parsing Methods =====
        
        ''' <summary>
        ''' Parse the entire document using centralized ProjectManager.Parser
        ''' </summary>
        Public Sub ParseDocument()
            Try
                ' Get full text
                Dim lFullText As String = GetAllText()
                
                ' Request ProjectManager through event
                Dim lProjectManager = GetProjectManager()
                If lProjectManager Is Nothing Then
                    Console.WriteLine("DocumentModel.ParseDocument: ProjectManager not available")
                    Return
                End If
                
                ' Use centralized ProjectParser to parse content
                Dim lParserProperty = lProjectManager.GetType().GetProperty("Parser")
                If lParserProperty Is Nothing Then
                    Console.WriteLine("DocumentModel.ParseDocument: Parser property not found")
                    Return
                End If
                
                Dim lParser = lParserProperty.GetValue(lProjectManager)
                If lParser Is Nothing Then
                    Console.WriteLine("DocumentModel.ParseDocument: ProjectParser not available")
                    Return
                End If
                
                ' Get RootNamespace from ProjectManager
                Dim lRootNamespaceProperty = lProjectManager.GetType().GetProperty("RootNamespace")
                Dim lRootNamespace As String = "SimpleIDE" ' Default
                If lRootNamespaceProperty IsNot Nothing Then
                    lRootNamespace = CStr(lRootNamespaceProperty.GetValue(lProjectManager))
                End If
                
                ' Parse using ProjectParser's ParseContent method via reflection
                Dim lParseContentMethod = lParser.GetType().GetMethod("ParseContent")
                If lParseContentMethod IsNot Nothing Then
                    Dim lParseResult = lParseContentMethod.Invoke(lParser, New Object() {lFullText, lRootNamespace, pFilePath})
                    
                    If lParseResult IsNot Nothing Then
                        ' Extract the root node from parse result
                        Dim lResultType = lParseResult.GetType()
                        Dim lRootNodeProperty = lResultType.GetProperty("RootNode")
                        
                        If lRootNodeProperty IsNot Nothing Then
                            Dim lRootNode = lRootNodeProperty.GetValue(lParseResult)
                            If lRootNode IsNot Nothing Then
                                ' Convert to DocumentNode if needed
                                pRootNode = ConvertToDocumentNode(lRootNode)
                                
                                ' Update node lookup
                                pNodeLookup.Clear()
                                BuildNodeLookup(pRootNode)
                                
                                ' Update line metadata with nodes
                                UpdateLineNodeReferences()
                                
                                ' Update parse state for all lines
                                For Each lLine In pDocumentLines
                                    If lLine.ParseState <> LineParseState.eBeingEdited Then
                                        lLine.ParseState = LineParseState.eParsed
                                    End If
                                Next
                                
                                ' Build indices
                                BuildIndices()
                                
                                ' Raise parsed event
                                RaiseEvent DocumentParsed(pRootNode)
                                
                                Console.WriteLine($"DocumentModel.ParseDocument: Successfully parsed {pFilePath}")
                            End If
                        End If
                        
                        ' Extract any errors
                        Dim lErrorsProperty = lResultType.GetProperty("Errors")
                        If lErrorsProperty IsNot Nothing Then
                            Dim lErrors = TryCast(lErrorsProperty.GetValue(lParseResult), IEnumerable)
                            If lErrors IsNot Nothing Then
                                For Each lError In lErrors
                                    Console.WriteLine($"Parse error: {lError}")
                                Next
                            End If
                        End If
                    End If
                Else
                    Console.WriteLine("DocumentModel.ParseDocument: ParseContent method not found")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.ParseDocument error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Build node lookup dictionary from root node
        ''' </summary>
        Private Sub BuildNodeLookup(vNode As DocumentNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Add to lookup if it has a full name
                If Not String.IsNullOrEmpty(vNode.FullName) Then
                    pNodeLookup(vNode.FullName) = vNode
                End If
                
                ' Process children recursively
                For Each lChild In vNode.Children
                    BuildNodeLookup(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"BuildNodeLookup error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Helper method to convert SyntaxNode to DocumentNode
        ''' </summary>
        Private Function ConvertToDocumentNode(vSyntaxNode As Object) As DocumentNode
            Try
                If vSyntaxNode Is Nothing Then Return Nothing
                
                Dim lDocNode As New DocumentNode()
                
                ' Use reflection to get properties from SyntaxNode
                Dim lType = vSyntaxNode.GetType()
                
                ' Copy basic properties
                Dim lNameProp = lType.GetProperty("Name")
                If lNameProp IsNot Nothing Then
                    lDocNode.Name = CStr(lNameProp.GetValue(vSyntaxNode))
                End If
                
                Dim lNodeTypeProp = lType.GetProperty("NodeType")
                If lNodeTypeProp IsNot Nothing Then
                    lDocNode.NodeType = CType(lNodeTypeProp.GetValue(vSyntaxNode), CodeNodeType)
                End If
                
                Dim lStartLineProp = lType.GetProperty("StartLine")
                If lStartLineProp IsNot Nothing Then
                    lDocNode.StartLine = CInt(lStartLineProp.GetValue(vSyntaxNode))
                End If
                
                Dim lEndLineProp = lType.GetProperty("EndLine")
                If lEndLineProp IsNot Nothing Then
                    lDocNode.EndLine = CInt(lEndLineProp.GetValue(vSyntaxNode))
                End If
                
                Dim lStartColumnProp = lType.GetProperty("StartColumn")
                If lStartColumnProp IsNot Nothing Then
                    lDocNode.StartColumn = CInt(lStartColumnProp.GetValue(vSyntaxNode))
                End If
                
                Dim lEndColumnProp = lType.GetProperty("EndColumn")
                If lEndColumnProp IsNot Nothing Then
                    lDocNode.EndColumn = CInt(lEndColumnProp.GetValue(vSyntaxNode))
                End If
                
                ' Copy children recursively
                Dim lChildrenProp = lType.GetProperty("Children")
                If lChildrenProp IsNot Nothing Then
                    Dim lChildren = TryCast(lChildrenProp.GetValue(vSyntaxNode), IEnumerable)
                    If lChildren IsNot Nothing Then
                        For Each lChild In lChildren
                            Dim lChildDoc = ConvertToDocumentNode(lChild)
                            If lChildDoc IsNot Nothing Then
                                lChildDoc.Parent = lDocNode
                                lDocNode.Children.Add(lChildDoc)
                            End If
                        Next
                    End If
                End If
                
                Return lDocNode
                
            Catch ex As Exception
                Console.WriteLine($"ConvertToDocumentNode error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Parse a single line for tokens
        ''' </summary>
        Private Function ParseLineTokens(vLineIndex As Integer) As List(Of SyntaxToken)
            Try
                Dim lTokens As New List(Of SyntaxToken)()
                
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then
                    Return lTokens
                End If
                
                Dim lLine As DocumentLine = pDocumentLines(vLineIndex)
                Dim lText As String = lLine.Text
                
                If String.IsNullOrEmpty(lText) Then
                    Return lTokens
                End If
                
                ' Simple tokenization for VB.NET
                ' Check for comment
                Dim lCommentIndex As Integer = lText.IndexOf("'")
                If lCommentIndex >= 0 Then
                    ' Everything after ' is a comment
                    If lCommentIndex > 0 Then
                        ' Parse text before comment
                        TokenizeLine(lTokens, lText.Substring(0, lCommentIndex), 0)
                    End If
                    ' Add comment token
                    lTokens.Add(New SyntaxToken(lCommentIndex, lText.Length - lCommentIndex, SyntaxTokenType.eComment))
                Else
                    ' No comment, tokenize entire line
                    TokenizeLine(lTokens, lText, 0)
                End If
                
                Return lTokens
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.ParseLineTokens error: {ex.Message}")
                Return New List(Of SyntaxToken)()
            End Try
        End Function
        
        Private Sub TokenizeLine(vTokens As List(Of SyntaxToken), vText As String, vStartOffset As Integer)
            Try
                ' VB.NET keywords
                Dim lKeywords As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                    "As", "Boolean", "Byte", "ByRef", "ByVal", "Case", "Catch", "Class", "Const",
                    "Dim", "Do", "Double", "each", "Else", "ElseIf", "End", "Enum", "Event",
                    "False", "Finally", "for", "Friend", "Function", "Get", "Handles", "If",
                    "Implements", "Imports", "in", "Inherits", "Integer", "Interface", "Is",
                    "Let", "Loop", "Me", "Module", "MyBase", "MyClass", "Namespace", "New",
                    "Next", "Not", "Nothing", "Object", "Of", "On", "Option", "Optional",
                    "Or", "OrElse", "Overloads", "Overridable", "Overrides", "ParamArray",
                    "Partial", "Private", "Property", "Protected", "Public", "RaiseEvent",
                    "ReadOnly", "ReDim", "Return", "Select", "Set", "Shared", "Single",
                    "Static", "Step", "String", "Structure", "Sub", "Then", "Throw", "To",
                    "True", "Try", "TypeOf", "Until", "Using", "When", "While", "with",
                    "WithEvents", "WriteOnly"
                }
                
                Dim lCurrentPos As Integer = 0
                Dim lInString As Boolean = False
                Dim lStringChar As Char = Nothing
                
                While lCurrentPos < vText.Length
                    Dim lChar As Char = vText(lCurrentPos)
                    
                    ' Skip whitespace
                    If Char.IsWhiteSpace(lChar) AndAlso Not lInString Then
                        lCurrentPos += 1
                        Continue While
                    End If
                    
                    ' Check for string literals
                    If lChar = """" Then
                        Dim lStringStart As Integer = lCurrentPos
                        lCurrentPos += 1
                        
                        ' Find end of string
                        While lCurrentPos < vText.Length
                            If vText(lCurrentPos) = """" Then
                                ' Check for double quote escape
                                If lCurrentPos + 1 < vText.Length AndAlso vText(lCurrentPos + 1) = """" Then
                                    lCurrentPos += 2 ' Skip escaped quote
                                Else
                                    lCurrentPos += 1
                                    Exit While
                                End If
                            Else
                                lCurrentPos += 1
                            End If
                        End While
                        
                        vTokens.Add(New SyntaxToken(vStartOffset + lStringStart, lCurrentPos - lStringStart, SyntaxTokenType.eString))
                        
                    ' Check for identifiers and keywords
                    ElseIf Char.IsLetter(lChar) OrElse lChar = "_" Then
                        Dim lIdentStart As Integer = lCurrentPos
                        
                        While lCurrentPos < vText.Length AndAlso (Char.IsLetterOrDigit(vText(lCurrentPos)) OrElse vText(lCurrentPos) = "_")
                            lCurrentPos += 1
                        End While
                        
                        Dim lIdentText As String = vText.Substring(lIdentStart, lCurrentPos - lIdentStart)
                        
                        If lKeywords.Contains(lIdentText) Then
                            vTokens.Add(New SyntaxToken(vStartOffset + lIdentStart, lCurrentPos - lIdentStart, SyntaxTokenType.eKeyword))
                        Else
                            vTokens.Add(New SyntaxToken(vStartOffset + lIdentStart, lCurrentPos - lIdentStart, SyntaxTokenType.eIdentifier))
                        End If
                        
                    ' Check for numbers
                    ElseIf Char.IsDigit(lChar) Then
                        Dim lNumberStart As Integer = lCurrentPos
                        
                        While lCurrentPos < vText.Length AndAlso (Char.IsDigit(vText(lCurrentPos)) OrElse vText(lCurrentPos) = ".")
                            lCurrentPos += 1
                        End While
                        
                        vTokens.Add(New SyntaxToken(vStartOffset + lNumberStart, lCurrentPos - lNumberStart, SyntaxTokenType.eNumber))
                        
                    ' Check for operators
                    ElseIf "+-*/=<>&|^()[]{},.".Contains(lChar) Then
                        vTokens.Add(New SyntaxToken(vStartOffset + lCurrentPos, 1, SyntaxTokenType.eOperator))
                        lCurrentPos += 1
                    Else
                        ' Skip other characters
                        lCurrentPos += 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.TokenizeLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parse a single line and update structure incrementally
        ''' </summary>
        Private Sub ParseLine(vLineIndex As Integer)
            Try
                If vLineIndex < 0 OrElse vLineIndex >= pDocumentLines.Count Then Return
                
                Dim lLine As DocumentLine = pDocumentLines(vLineIndex)
                
                ' Re-tokenize the line
                Dim lTokens As List(Of SyntaxToken) = ParseLineTokens(vLineIndex)
                lLine.Tokens = lTokens
                pTokenCache(vLineIndex) = lTokens
                
                ' Update parse state
                lLine.ParseState = LineParseState.eParsed
                lLine.LastParsedVersion = pDocumentVersion
                
                ' Notify rendering change
                RaiseEvent LineRenderingChanged(vLineIndex, LineRenderMode.eNormal)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.ParseLine error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Update structure incrementally after line edit
        ''' </summary>
        Private Sub UpdateIncrementalStructure(vLineIndex As Integer)
            Try
                ' For now, do a full reparse
                ' TODO: Implement incremental parsing for better performance
                ParseDocument()
                BuildIndices()
                
                ' Notify about structure change
                Dim lAffectedNodes As New List(Of DocumentNode)()
                If pRootNode IsNot Nothing Then
                    CollectAllNodes(pRootNode, lAffectedNodes)
                End If
                
                RaiseEvent StructureChanged(lAffectedNodes)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.UpdateIncrementalStructure error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        Private Sub SetModified(vModified As Boolean)
            Try
                If pIsModified <> vModified Then
                    pIsModified = vModified
                    RaiseEvent ModifiedStateChanged(pIsModified)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.SetModified error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub InsertMultilineText(vLineIndex As Integer, vColumn As Integer, vText As String)
            Try
                ' Split text into lines
                Dim lLines As String() = vText.Replace(vbCrLf, vbLf).Split(vbLf)
                If lLines.Length = 0 Then Return
                
                Dim lOriginalLine As DocumentLine = pDocumentLines(vLineIndex)
                Dim lOriginalText As String = lOriginalLine.Text
                
                ' First line: append to current line
                Dim lBeforeCursor As String = lOriginalText.Substring(0, vColumn)
                Dim lAfterCursor As String = lOriginalText.Substring(vColumn)
                
                ' Update first line
                lOriginalLine.Text = lBeforeCursor & lLines(0)
                lOriginalLine.MarkChanged()
                
                ' Insert middle lines
                For i As Integer = 1 To lLines.Length - 2
                    InsertLine(vLineIndex + i, lLines(i))
                Next
                
                ' Last line
                If lLines.Length > 1 Then
                    InsertLine(vLineIndex + lLines.Length - 1, lLines(lLines.Length - 1) & lAfterCursor)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.InsertMultilineText error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateLineNodeReferences()
            Try
                ' Clear all line node references
                For Each lLine In pDocumentLines
                    lLine.Nodes.Clear()
                Next
                
                ' Add node references to appropriate lines
                If pRootNode IsNot Nothing Then
                    AddNodeReferencesToLines(pRootNode)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.UpdateLineNodeReferences error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AddNodeReferencesToLines(vNode As DocumentNode)
            Try
                If vNode Is Nothing Then Return
                
                ' Add this node to its start line
                If vNode.StartLine >= 0 AndAlso vNode.StartLine < pDocumentLines.Count Then
                    pDocumentLines(vNode.StartLine).Nodes.Add(vNode)
                End If
                
                ' Recursively process children
                For Each lChild In vNode.Children
                    AddNodeReferencesToLines(lChild)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AddNodeReferencesToLines error: {ex.Message}")
            End Try
        End Sub
        
        Private Function FindNodeAtPosition(vNode As DocumentNode, vLine As Integer, vColumn As Integer) As DocumentNode
            Try
                If vNode Is Nothing Then Return Nothing
                
                ' Check if position is within this node
                If Not vNode.ContainsPosition(vLine, vColumn) Then
                    Return Nothing
                End If
                
                ' Check children for more specific match
                For Each lChild In vNode.Children
                    Dim lChildMatch As DocumentNode = FindNodeAtPosition(lChild, vLine, vColumn)
                    If lChildMatch IsNot Nothing Then
                        Return lChildMatch
                    End If
                Next
                
                ' This node is the most specific match
                Return vNode
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.FindNodeAtPosition error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Sub AdjustNodeCoordinatesAfterInsert(vLineIndex As Integer, vColumn As Integer, vLength As Integer)
            Try
                If pRootNode Is Nothing Then Return
                
                ' Adjust all nodes on the same line after the insertion point
                AdjustNodesRecursive(pRootNode, vLineIndex, vColumn, vLength, False)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AdjustNodeCoordinatesAfterInsert error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AdjustNodeCoordinatesAfterDelete(vLineIndex As Integer, vColumn As Integer, vLength As Integer)
            Try
                If pRootNode Is Nothing Then Return
                
                ' Adjust all nodes on the same line after the deletion point
                AdjustNodesRecursive(pRootNode, vLineIndex, vColumn, -vLength, False)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AdjustNodeCoordinatesAfterDelete error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AdjustNodeCoordinatesAfterLineInsert(vLineIndex As Integer)
            Try
                If pRootNode Is Nothing Then Return
                
                ' Adjust all nodes after the inserted line
                AdjustNodesForLineInsert(pRootNode, vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AdjustNodeCoordinatesAfterLineInsert error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AdjustNodeCoordinatesAfterLineDelete(vLineIndex As Integer)
            Try
                If pRootNode Is Nothing Then Return
                
                ' Adjust all nodes after the deleted line
                AdjustNodesForLineDelete(pRootNode, vLineIndex)
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AdjustNodeCoordinatesAfterLineDelete error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AdjustNodesRecursive(vNode As DocumentNode, vLineIndex As Integer, vColumn As Integer, vDelta As Integer, vIsLineAdjust As Boolean)
            Try
                If vNode Is Nothing Then Return
                
                If vIsLineAdjust Then
                    ' Adjust for line insertion/deletion
                    If vNode.StartLine > vLineIndex Then
                        vNode.StartLine += vDelta
                    End If
                    If vNode.EndLine > vLineIndex Then
                        vNode.EndLine += vDelta
                    End If
                Else
                    ' Adjust for character insertion/deletion
                    If vNode.StartLine = vLineIndex AndAlso vNode.StartColumn > vColumn Then
                        vNode.StartColumn += vDelta
                    End If
                    If vNode.EndLine = vLineIndex AndAlso vNode.EndColumn > vColumn Then
                        vNode.EndColumn += vDelta
                    End If
                End If
                
                ' Recursively adjust children
                For Each lChild In vNode.Children
                    AdjustNodesRecursive(lChild, vLineIndex, vColumn, vDelta, vIsLineAdjust)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AdjustNodesRecursive error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AdjustNodesForLineInsert(vNode As DocumentNode, vLineIndex As Integer)
            Try
                If vNode Is Nothing Then Return
                
                ' Adjust for line insertion
                If vNode.StartLine > vLineIndex Then
                    vNode.StartLine += 1
                End If
                If vNode.EndLine >= vLineIndex Then
                    vNode.EndLine += 1
                End If
                
                ' Recursively adjust children
                For Each lChild In vNode.Children
                    AdjustNodesForLineInsert(lChild, vLineIndex)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AdjustNodesForLineInsert error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AdjustNodesForLineDelete(vNode As DocumentNode, vLineIndex As Integer)
            Try
                If vNode Is Nothing Then Return
                
                ' Adjust for line deletion
                If vNode.StartLine > vLineIndex Then
                    vNode.StartLine -= 1
                End If
                If vNode.EndLine > vLineIndex Then
                    vNode.EndLine -= 1
                ElseIf vNode.EndLine = vLineIndex Then
                    ' Node ends on deleted line, adjust to previous line
                    vNode.EndLine = Math.Max(0, vLineIndex - 1)
                End If
                
                ' Recursively adjust children
                For Each lChild In vNode.Children
                    AdjustNodesForLineDelete(lChild, vLineIndex)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.AdjustNodesForLineDelete error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CollectAllNodes(vNode As DocumentNode, vList As List(Of DocumentNode))
            Try
                If vNode Is Nothing Then Return
                
                vList.Add(vNode)
                
                For Each lChild In vNode.Children
                    CollectAllNodes(lChild, vList)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.CollectAllNodes error: {ex.Message}")
            End Try
        End Sub
        
        Private Function FindContainingMethod(vLine As Integer) As DocumentNode
            Try
                Dim lNode As DocumentNode = GetNodeAtPosition(vLine, 0)
                
                While lNode IsNot Nothing
                    Select Case lNode.NodeType
                        Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor
                            Return lNode
                    End Select
                    lNode = lNode.Parent
                End While
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.FindContainingMethod error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Sub CollectVariablesInScope(vScope As ScopeInfo, vLine As Integer)
            Try
                vScope.VariablesInScope.Clear()
                
                ' Collect parameters if in a method
                If vScope.ContainingMethod IsNot Nothing Then
                    For Each lChild In vScope.ContainingMethod.Children
                        If lChild.NodeType = CodeNodeType.eParameter Then
                            vScope.VariablesInScope.Add(lChild)
                        End If
                    Next
                End If
                
                ' Collect local variables declared before current line
                If vScope.ContainingMethod IsNot Nothing Then
                    For Each lChild In vScope.ContainingMethod.Children
                        If lChild.NodeType = CodeNodeType.eVariable AndAlso lChild.StartLine < vLine Then
                            vScope.VariablesInScope.Add(lChild)
                        End If
                    Next
                End If
                
                ' Collect class fields
                If vScope.ContainingClass IsNot Nothing Then
                    For Each lChild In vScope.ContainingClass.Children
                        If lChild.NodeType = CodeNodeType.eField Then
                            vScope.VariablesInScope.Add(lChild)
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.CollectVariablesInScope error: {ex.Message}")
            End Try
        End Sub
        
        Private Function IsDeclarationNode(vNodeType As CodeNodeType) As Boolean
            Select Case vNodeType
                Case CodeNodeType.eClass, CodeNodeType.eModule, CodeNodeType.eInterface,
                     CodeNodeType.eStructure, CodeNodeType.eEnum, CodeNodeType.eMethod,
                     CodeNodeType.eFunction, CodeNodeType.eProperty, CodeNodeType.eField,
                     CodeNodeType.eEvent, CodeNodeType.eConstructor
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        ''' <summary>
        ''' Gets the ProjectManager through event-based request with caching
        ''' </summary>
        Private Function GetProjectManager() As Object
            Try
                ' Return cached reference if available
                If pCachedProjectManager IsNot Nothing Then
                    Return pCachedProjectManager
                End If
                
                ' Request ProjectManager through event
                Dim lArgs As New ProjectManagerRequestEventArgs()
                RaiseEvent RequestProjectManager(Me, lArgs)
                
                If lArgs.HasProjectManager Then
                    ' Cache the reference for future use
                    pCachedProjectManager = lArgs.ProjectManager
                    Return pCachedProjectManager
                End If
                
                Console.WriteLine("DocumentModel: No ProjectManager provided via event")
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetProjectManager error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Subscribe to ProjectManager parse completion events
        ''' </summary>
        Private Sub SubscribeToProjectManagerEvents()
            Try
                ' Request ProjectManager through event
                Dim lProjectManager = GetProjectManager()
                If lProjectManager Is Nothing Then
                    Console.WriteLine("DocumentModel: ProjectManager not available for event subscription")
                    Return
                End If
                
                ' Subscribe to parse completed event using late binding
                Dim lProjectManagerType = lProjectManager.GetType()
                Dim lParseCompletedEvent = lProjectManagerType.GetEvent("ParseCompleted")
                
                If lParseCompletedEvent IsNot Nothing Then
                    Dim lHandler = [Delegate].CreateDelegate(
                        lParseCompletedEvent.EventHandlerType,
                        Me,
                        GetType(DocumentModel).GetMethod("OnProjectManagerParseCompleted", 
                            Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance))
                    
                    lParseCompletedEvent.RemoveEventHandler(lProjectManager, lHandler)
                    lParseCompletedEvent.AddEventHandler(lProjectManager, lHandler)
                    
                    Console.WriteLine($"DocumentModel subscribed to ProjectManager.ParseCompleted for {pFilePath}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SubscribeToProjectManagerEvents error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handle parse completion from ProjectManager
        ''' </summary>
        Private Sub OnProjectManagerParseCompleted(vFile As SourceFileInfo, vResult As Object)
            Try
                ' Check if this is our file
                If vFile Is Nothing OrElse Not String.Equals(vFile.FilePath, pFilePath, StringComparison.OrdinalIgnoreCase) Then
                    Return
                End If
                
                Console.WriteLine($"DocumentModel received ParseCompleted for {pFilePath}")
                
                ' Extract and convert the parse result
                If vResult IsNot Nothing Then
                    Dim lResultType = vResult.GetType()
                    Dim lRootNodeProperty = lResultType.GetProperty("RootNode")
                    
                    If lRootNodeProperty IsNot Nothing Then
                        Dim lRootNode = lRootNodeProperty.GetValue(vResult)
                        If lRootNode IsNot Nothing Then
                            ' Convert to DocumentNode if needed
                            pRootNode = ConvertToDocumentNode(lRootNode)
                            
                            ' Update node lookup
                            pNodeLookup.Clear()
                            BuildNodeLookup(pRootNode)
                            
                            ' Update line metadata with nodes
                            UpdateLineNodeReferences()
                            
                            ' Update parse state for all lines
                            For Each lLine In pDocumentLines
                                If lLine.ParseState <> LineParseState.eBeingEdited Then
                                    lLine.ParseState = LineParseState.eParsed
                                End If
                            Next
                            
                            ' Build indices
                            BuildIndices()
                            
                            ' Raise parsed event
                            RaiseEvent DocumentParsed(pRootNode)
                            
                            Console.WriteLine($"DocumentModel updated from ParseCompleted: {pFilePath}")
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectManagerParseCompleted error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Clean up resources and unsubscribe from events
        ''' </summary>
        Public Sub Dispose()
            Try
                ' Unsubscribe from ProjectManager events
                UnsubscribeFromProjectManagerEvents()
                
                ' Clear collections
                If pDocumentLines IsNot Nothing Then
                    pDocumentLines.Clear()
                End If
                
                If pNodeLookup IsNot Nothing Then
                    pNodeLookup.Clear()
                End If
                
                If pSymbolIndex IsNot Nothing Then
                    pSymbolIndex.Clear()
                End If
                
                If pMethodBoundaryCache IsNot Nothing Then
                    pMethodBoundaryCache.Clear()
                End If
                
                If pScopeCache IsNot Nothing Then
                    pScopeCache.Clear()
                End If
                
                If pAttachedEditors IsNot Nothing Then
                    pAttachedEditors.Clear()
                End If
                
                If pTokenCache IsNot Nothing Then
                    pTokenCache.Clear()
                End If
                
                If pDeclarationRegistry IsNot Nothing Then
                    pDeclarationRegistry.Clear()
                End If
                
                If pCrossReferences IsNot Nothing Then
                    pCrossReferences.Clear()
                End If
                
                ' Clear root node
                pRootNode = Nothing
                
                Console.WriteLine($"DocumentModel disposed: {pFilePath}")
                
            Catch ex As Exception
                Console.WriteLine($"DocumentModel.Dispose error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Unsubscribe from ProjectManager events
        ''' </summary>
        Private Sub UnsubscribeFromProjectManagerEvents()
            Try
                ' Request ProjectManager through event
                Dim lProjectManager = GetProjectManager()
                If lProjectManager Is Nothing Then
                    Return
                End If
                
                ' Unsubscribe from parse completed event using late binding
                Dim lProjectManagerType = lProjectManager.GetType()
                Dim lParseCompletedEvent = lProjectManagerType.GetEvent("ParseCompleted")
                
                If lParseCompletedEvent IsNot Nothing Then
                    Dim lHandler = [Delegate].CreateDelegate(
                        lParseCompletedEvent.EventHandlerType,
                        Me,
                        GetType(DocumentModel).GetMethod("OnProjectManagerParseCompleted", 
                            Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance))
                    
                    lParseCompletedEvent.RemoveEventHandler(lProjectManager, lHandler)
                    
                    Console.WriteLine($"DocumentModel unsubscribed from ProjectManager events for {pFilePath}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UnsubscribeFromProjectManagerEvents error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
    ' ===== DocumentLine Class =====
    
    ''' <summary>
    ''' Represents a single line in the document with rich metadata
    ''' </summary>
    Public Class DocumentLine
        
        Public Property Text As String = ""
        Public Property Tokens As New List(Of SyntaxToken)()
        Public Property Nodes As New List(Of DocumentNode)()
        Public Property ParseState As DocumentModel.LineParseState = DocumentModel.LineParseState.eUnparsed
        Public Property IsBeingEdited As Boolean = False
        Public Property LastParsedVersion As Integer = 0
        Public Property LineNumber As Integer = 0
        Public Property ContainingScope As ScopeInfo
        Public Property ModificationTimestamp As DateTime = DateTime.Now
        Public Property BreakpointCapable As Boolean = False
        Public Property HasBreakpoint As Boolean = False
        Public Property ExecutionCount As Integer = 0
        
        ''' <summary>
        ''' Mark this line as changed
        ''' </summary>
        Public Sub MarkChanged()
            ParseState = DocumentModel.LineParseState.eUnparsed
            Tokens.Clear()
            ModificationTimestamp = DateTime.Now
        End Sub
        
    End Class
    
    ' ===== Supporting Classes =====
    
    ''' <summary>
    ''' Information about a declaration
    ''' </summary>
    Public Class DeclarationInfo
        Public Property Node As DocumentNode
        Public Property Name As String
        Public Property FullName As String
        Public Property DeclarationType As CodeNodeType
        Public Property Line As Integer
        Public Property Column As Integer
        Public Property IsPublic As Boolean = True
        Public Property ReturnType As String = ""
        Public Property Parameters As New List(Of String)()
    End Class
    
    ''' <summary>
    ''' Information about a reference to a symbol
    ''' </summary>
    Public Class ReferenceInfo
        Public Property SymbolName As String
        Public Property Line As Integer
        Public Property Column As Integer
        Public Property ReferenceType As ReferenceType
        Public Property ContainingNode As DocumentNode
    End Class
    
    ''' <summary>
    ''' Type of reference
    ''' </summary>
    Public Enum ReferenceType
        eUnspecified
        eMethodCall
        ePropertyAccess
        eFieldAccess
        eTypeReference
        eVariableReference
        eLastValue
    End Enum
    
    ''' <summary>
    ''' Information about scope at a position
    ''' </summary>
    Public Class ScopeInfo
        Public Property Line As Integer
        Public Property ContainingNode As DocumentNode
        Public Property ContainingClass As DocumentNode
        Public Property ContainingMethod As DocumentNode
        Public Property VariablesInScope As New List(Of DocumentNode)()
        Public Property AvailableMembers As New List(Of DocumentNode)()
        
        Public ReadOnly Property IsInClassScope As Boolean
            Get
                Return ContainingClass IsNot Nothing
            End Get
        End Property
        
        Public ReadOnly Property IsInMethodScope As Boolean
            Get
                Return ContainingMethod IsNot Nothing
            End Get
        End Property
    End Class
    
End Namespace
