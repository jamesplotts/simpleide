' Syntax/VBParser.Unified.vb - Unified recursive VB.NET parser
Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models

' VBParser.Unified.vb
' Created: 2025-08-13

Namespace Syntax
    
    ''' <summary>
    ''' VB.NET parser with recursive block-based parsing for complete hierarchy
    ''' </summary>
    Public Class VBParser
        
        ' ===== Public Parse Result =====
        
        Public Class ParseResult
            Public Property RootNode As SyntaxNode
            Public Property Errors As New List(Of ParseError)()
            Public Property DocumentNodes As New Dictionary(Of String, DocumentNode)()
            Public Property RootNodes As New List(Of DocumentNode)()
            Public Property LineMetadata As LineMetadata()
        End Class
        
        ' ===== Block Information Structure =====
        
        Private Structure BlockInfo
            Public StartLine As Integer
            Public EndLine As Integer
            Public BlockType As String
            Public Name As String
            Public Modifiers As String
            Public FullLine As String
        End Structure
        
        ' ===== Private Fields =====
        
        Private pLines As String()
        Private pCurrentLine As Integer
        Private pErrors As New List(Of ParseError)()
        Private pRootNode As SyntaxNode
        Private pCurrentFilePath As String
        Private pCurrentFileName As String
        Private pProjectRootNamespace As SyntaxNode ' Shared across all files in project
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' The root namespace for the project (e.g., "SimpleIDE")
        ''' </summary>
        Public Property RootNamespace As String = "SimpleIDE"
        
        ' ===== Main Parse Method =====
        
        ''' <summary>
        ''' Parse VB.NET source code into a hierarchical syntax tree
        ''' </summary>
        Public Function Parse(vContent As String, vRootNamespace As String, vFilePath As String) As ParseResult
            Try
                Console.WriteLine($"=== VBParser.Parse START: {vFilePath} ===")
                
                ' Initialize
                pCurrentFilePath = vFilePath
                pCurrentFileName = System.IO.Path.GetFileName(vFilePath)
                pLines = vContent.Split({Environment.NewLine, vbLf, vbCr}, StringSplitOptions.None)
                pErrors.Clear()
                
                ' Update root namespace if provided
                If Not String.IsNullOrEmpty(vRootNamespace) Then
                    RootNamespace = vRootNamespace
                End If
                
                ' Create document root
                pRootNode = New SyntaxNode(CodeNodeType.eDocument, "document")
                pRootNode.StartLine = 0
                pRootNode.EndLine = pLines.Length - 1
                
                ' Get or create the root namespace node (for partial class merging)
                Dim lRootNamespaceNode As SyntaxNode = GetOrCreateRootNamespace(RootNamespace)
                
                ' Find all top-level blocks in the file
                Dim lTopLevelBlocks As List(Of BlockInfo) = FindTopLevelBlocks()
                
                Console.WriteLine($"Found {lTopLevelBlocks.Count} top-level blocks")
                
                ' Process each top-level block
                For Each lBlock In lTopLevelBlocks
                    ProcessBlock(lBlock, lRootNamespaceNode)
                Next
                
                ' Handle any top-level members not in blocks (rare but possible)
                ProcessTopLevelMembers(lRootNamespaceNode)
                
                ' Create the result
                Dim lResult As New ParseResult()
                lResult.RootNode = pRootNode
                lResult.Errors = pErrors
                
                Console.WriteLine($"=== VBParser.Parse END: {GetNodeSummary(pRootNode)} ===")
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"Parse error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
                
                ' Return partial result even on error
                Dim lResult As New ParseResult()
                lResult.RootNode = pRootNode
                lResult.Errors = pErrors
                lResult.Errors.Add(New ParseError With {
                    .Message = $"Fatal parse error: {ex.Message}",
                    .Line = 0,
                    .Column = 0,
                    .Severity = ParseErrorSeverity.eError
                })
                Return lResult
            End Try
        End Function
        
        ' ===== Project-Level Parsing =====
        
        ''' <summary>
        ''' Parse multiple files for a project, handling partial classes
        ''' </summary>
        Public Function ParseProject(vFiles As List(Of SourceFileInfo), vRootNamespaceName As String) As SyntaxNode
            Try
                Console.WriteLine($"=== ParseProject START: {vFiles.Count} files ===")
                
                ' Create the project root namespace
                pProjectRootNamespace = New SyntaxNode(CodeNodeType.eNamespace, vRootNamespaceName)
                pProjectRootNamespace.IsImplicit = True
                
                ' Parse each file
                For Each lFile In vFiles
                    Console.WriteLine($"Parsing file: {lFile.FilePath}")
                    
                    Try
                        ' Parse the file
                        Dim lResult As ParseResult = Parse(lFile.Content, vRootNamespaceName, lFile.FilePath)
                        
                        If lResult IsNot Nothing AndAlso lResult.RootNode IsNot Nothing Then
                            Console.WriteLine($"  Successfully parsed {lFile.FileName}")
                            ' Update the file's syntax tree
                            lFile.SyntaxTree = lResult.RootNode
                            lFile.ParseErrors = lResult.Errors
                            lFile.LastParsed = DateTime.Now
                            lFile.NeedsParsing = False
                        Else
                            Console.WriteLine($"  Failed to parse {lFile.FileName}")
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"  Error parsing {lFile.FileName}: {ex.Message}")
                    End Try
                Next
                
                Console.WriteLine($"=== ParseProject END ===")
                
                ' Return the merged project structure
                Return pProjectRootNamespace
                
            Catch ex As Exception
                Console.WriteLine($"ParseProject error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' ===== Root Namespace Management =====
        
        Private Function GetOrCreateRootNamespace(vNamespaceName As String) As SyntaxNode
            Try
                ' If we have an existing project root namespace (from ParseProject), use it
                If pProjectRootNamespace IsNot Nothing AndAlso 
                   pProjectRootNamespace.Name = vNamespaceName Then
                    Console.WriteLine($"Using existing project root namespace: {vNamespaceName}")
                    ' Make sure it's in the document
                    If Not pRootNode.Children.Contains(pProjectRootNamespace) Then
                        pRootNode.AddChild(pProjectRootNamespace)
                    End If
                    Return pProjectRootNamespace
                End If
                
                ' Check if root namespace already exists in document
                For Each lChild In pRootNode.Children
                    If lChild.NodeType = CodeNodeType.eNamespace AndAlso 
                       lChild.Name = vNamespaceName AndAlso 
                       lChild.IsImplicit Then
                        Console.WriteLine($"Found existing root namespace in document: {vNamespaceName}")
                        Return lChild
                    End If
                Next
                
                ' Create new root namespace
                Dim lNamespaceNode As New SyntaxNode(CodeNodeType.eNamespace, vNamespaceName)
                lNamespaceNode.StartLine = 0
                lNamespaceNode.EndLine = pLines.Length - 1
                lNamespaceNode.IsImplicit = True
                
                pRootNode.AddChild(lNamespaceNode)
                
                Console.WriteLine($"Created root namespace: {vNamespaceName}")
                Return lNamespaceNode
                
            Catch ex As Exception
                Console.WriteLine($"GetOrCreateRootNamespace error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' ===== Block Detection =====
        
        Private Function FindTopLevelBlocks() As List(Of BlockInfo)
            Dim lBlocks As New List(Of BlockInfo)()
            Dim lCurrentLine As Integer = 0
            
            Try
                While lCurrentLine < pLines.Length
                    Dim lLine As String = pLines(lCurrentLine).Trim()
                    
                    ' Skip empty lines and comments
                    If String.IsNullOrWhiteSpace(lLine) OrElse lLine.StartsWith("'") Then
                        lCurrentLine += 1
                        Continue While
                    End If
                    
                    ' Skip Imports and Option statements
                    If lLine.StartsWith("Imports ", StringComparison.OrdinalIgnoreCase) OrElse
                       lLine.StartsWith("Option ", StringComparison.OrdinalIgnoreCase) Then
                        lCurrentLine += 1
                        Continue While
                    End If
                    
                    ' Check for block start
                    Dim lBlock As BlockInfo = DetectBlockStart(lCurrentLine)
                    If lBlock.BlockType IsNot Nothing Then
                        ' Find the end of this block
                        lBlock.EndLine = FindBlockEnd(lCurrentLine, lBlock.BlockType)
                        lBlocks.Add(lBlock)
                        
                        ' Skip to after the block
                        lCurrentLine = lBlock.EndLine + 1
                    Else
                        lCurrentLine += 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"FindTopLevelBlocks error: {ex.Message}")
            End Try
            
            Return lBlocks
        End Function
        

        
        Private Function ExtractTypeName(vWords() As String, vStartIndex As Integer) As String
            Try
                ' Get the name, handling generics like MyClass(Of T)
                Dim lName As String = vWords(vStartIndex)
                
                ' Remove any trailing parenthesis from generics
                Dim lParenIndex As Integer = lName.IndexOf("("c)
                If lParenIndex > 0 Then
                    lName = lName.Substring(0, lParenIndex)
                End If
                
                Return lName
                
            Catch ex As Exception
                Console.WriteLine($"ExtractTypeName error: {ex.Message}")
                Return "Unknown"
            End Try
        End Function
        

        

        

        
        Private Function ProcessClassBlock(vBlock As BlockInfo, vParentNode As SyntaxNode) As SyntaxNode
            Try
                Dim lIsPartial As Boolean = vBlock.Modifiers.Contains("Partial")
                
                ' Check if class already exists (partial class from another file)
                If lIsPartial Then
                    Dim lExistingClass As SyntaxNode = FindChildByNameAndType(vParentNode, vBlock.Name, CodeNodeType.eClass)
                    
                    If lExistingClass IsNot Nothing Then
                        Console.WriteLine($"  Found existing partial class: {vBlock.Name}")
                        lExistingClass.IsPartial = True
                        
                        ' Track this file in the class attributes
                        If lExistingClass.Attributes Is Nothing Then
                            lExistingClass.Attributes = New Dictionary(Of String, String)()
                        End If
                        
                        If Not lExistingClass.Attributes.ContainsKey("FilePaths") Then
                            lExistingClass.Attributes("FilePaths") = pCurrentFilePath
                        Else
                            Dim lPaths As String = lExistingClass.Attributes("FilePaths")
                            If Not lPaths.Contains(pCurrentFilePath) Then
                                lExistingClass.Attributes("FilePaths") = lPaths & ";" & pCurrentFilePath
                            End If
                        End If
                        
                        Return lExistingClass
                    End If
                End If
                
                ' Create new class
                Dim lClassNode As New SyntaxNode(CodeNodeType.eClass, vBlock.Name)
                lClassNode.StartLine = vBlock.StartLine
                lClassNode.EndLine = vBlock.EndLine
                lClassNode.IsPartial = lIsPartial
                StoreFileInfo(lClassNode)
                SetModifiers(lClassNode, vBlock.Modifiers)
                
                ' Parse inheritance/implements from the full line
                ParseInheritanceAndImplements(lClassNode, vBlock.FullLine)
                
                vParentNode.AddChild(lClassNode)
                Console.WriteLine($"  Created class: {vBlock.Name}{If(lIsPartial, " (partial)", "")}")
                
                Return lClassNode
                
            Catch ex As Exception
                Console.WriteLine($"ProcessClassBlock error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function ProcessModuleBlock(vBlock As BlockInfo, vParentNode As SyntaxNode) As SyntaxNode
            Try
                Dim lIsPartial As Boolean = vBlock.Modifiers.Contains("Partial")
                
                ' Check if module already exists (partial module)
                If lIsPartial Then
                    Dim lExistingModule As SyntaxNode = FindChildByNameAndType(vParentNode, vBlock.Name, CodeNodeType.eModule)
                    
                    If lExistingModule IsNot Nothing Then
                        Console.WriteLine($"  Found existing partial module: {vBlock.Name}")
                        lExistingModule.IsPartial = True
                        StoreFileInfo(lExistingModule)
                        Return lExistingModule
                    End If
                End If
                
                ' Create new module
                Dim lModuleNode As New SyntaxNode(CodeNodeType.eModule, vBlock.Name)
                lModuleNode.StartLine = vBlock.StartLine
                lModuleNode.EndLine = vBlock.EndLine
                lModuleNode.IsPartial = lIsPartial
                StoreFileInfo(lModuleNode)
                SetModifiers(lModuleNode, vBlock.Modifiers)
                
                vParentNode.AddChild(lModuleNode)
                Console.WriteLine($"  Created module: {vBlock.Name}")
                
                Return lModuleNode
                
            Catch ex As Exception
                Console.WriteLine($"ProcessModuleBlock error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function ProcessInterfaceBlock(vBlock As BlockInfo, vParentNode As SyntaxNode) As SyntaxNode
            Try
                ' Create new interface
                Dim lInterfaceNode As New SyntaxNode(CodeNodeType.eInterface, vBlock.Name)
                lInterfaceNode.StartLine = vBlock.StartLine
                lInterfaceNode.EndLine = vBlock.EndLine
                StoreFileInfo(lInterfaceNode)
                SetModifiers(lInterfaceNode, vBlock.Modifiers)
                
                ' Parse inheritance from the full line
                ParseInheritanceAndImplements(lInterfaceNode, vBlock.FullLine)
                
                vParentNode.AddChild(lInterfaceNode)
                Console.WriteLine($"  Created interface: {vBlock.Name}")
                
                Return lInterfaceNode
                
            Catch ex As Exception
                Console.WriteLine($"ProcessInterfaceBlock error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function ProcessStructureBlock(vBlock As BlockInfo, vParentNode As SyntaxNode) As SyntaxNode
            Try
                ' Create new structure
                Dim lStructureNode As New SyntaxNode(CodeNodeType.eStructure, vBlock.Name)
                lStructureNode.StartLine = vBlock.StartLine
                lStructureNode.EndLine = vBlock.EndLine
                StoreFileInfo(lStructureNode)
                SetModifiers(lStructureNode, vBlock.Modifiers)
                
                ' Parse implements from the full line
                ParseInheritanceAndImplements(lStructureNode, vBlock.FullLine)
                
                vParentNode.AddChild(lStructureNode)
                Console.WriteLine($"  Created structure: {vBlock.Name}")
                
                Return lStructureNode
                
            Catch ex As Exception
                Console.WriteLine($"ProcessStructureBlock error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function ProcessEnumBlock(vBlock As BlockInfo, vParentNode As SyntaxNode) As SyntaxNode
            Try
                ' Create new enum
                Dim lEnumNode As New SyntaxNode(CodeNodeType.eEnum, vBlock.Name)
                lEnumNode.StartLine = vBlock.StartLine
                lEnumNode.EndLine = vBlock.EndLine
                StoreFileInfo(lEnumNode)
                SetModifiers(lEnumNode, vBlock.Modifiers)
                
                vParentNode.AddChild(lEnumNode)
                Console.WriteLine($"  Created enum: {vBlock.Name}")
                
                Return lEnumNode
                
            Catch ex As Exception
                Console.WriteLine($"ProcessEnumBlock error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' ===== Parse Node Contents (Recursive) =====
        
        Private Sub ParseNodeContents(vNode As SyntaxNode, vStartLine As Integer, vEndLine As Integer)
            Try
                If vStartLine > vEndLine OrElse vStartLine < 0 OrElse vEndLine >= pLines.Length Then
                    Return
                End If
                
                Console.WriteLine($"  Parsing contents of {vNode.Name} (lines {vStartLine}-{vEndLine})")
                
                Dim lCurrentLine As Integer = vStartLine
                
                While lCurrentLine <= vEndLine
                    Dim lLine As String = pLines(lCurrentLine).Trim()
                    
                    ' Skip empty lines and comments
                    If String.IsNullOrWhiteSpace(lLine) OrElse lLine.StartsWith("'") Then
                        lCurrentLine += 1
                        Continue While
                    End If
                    
                    ' Check for nested blocks
                    Dim lBlock As BlockInfo = DetectBlockStart(lCurrentLine)
                    If lBlock.BlockType IsNot Nothing Then
                        ' Find the end of this nested block
                        lBlock.EndLine = FindBlockEnd(lCurrentLine, lBlock.BlockType)
                        
                        ' Process the nested block
                        ProcessBlock(lBlock, vNode)
                        
                        ' Skip to after the block
                        lCurrentLine = lBlock.EndLine + 1
                    Else
                        ' Parse as a member (method, property, field, etc.)
                        Dim lEndLine As Integer = ParseMember(vNode, lCurrentLine)
                        lCurrentLine = lEndLine + 1
                    End If
                End While
                
            Catch ex As Exception
                Console.WriteLine($"ParseNodeContents error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Member Parsing =====
        
        Private Function ParseMember(vParentNode As SyntaxNode, vLineIndex As Integer) As Integer
            Try
                Dim lLine As String = pLines(vLineIndex)
                Dim lTrimmedLine As String = lLine.Trim()
                
                ' Skip empty lines and comments
                If String.IsNullOrWhiteSpace(lTrimmedLine) OrElse lTrimmedLine.StartsWith("'") Then
                    Return vLineIndex
                End If
                
                ' Split into words
                Dim lWords As String() = lTrimmedLine.Split({" "c, vbTab, "("c}, StringSplitOptions.RemoveEmptyEntries)
                If lWords.Length = 0 Then Return vLineIndex
                
                Dim lIndex As Integer = 0
                Dim lModifiers As New List(Of String)()
                
                ' Collect modifiers
                While lIndex < lWords.Length
                    Select Case lWords(lIndex).ToUpper()
                        Case "PUBLIC", "PRIVATE", "FRIEND", "PROTECTED", "SHARED", _
                             "OVERRIDES", "OVERRIDABLE", "MUSTOVERRIDE", "NOTOVERRIDABLE", _
                             "READONLY", "WRITEONLY", "WITHEVENTS", "DIM", "SHADOWS", _
                             "OVERLOADS", "STATIC", "ASYNC", "ITERATOR"
                            lModifiers.Add(lWords(lIndex))
                            lIndex += 1
                        Case Else
                            Exit While
                    End Select
                End While
                
                If lIndex >= lWords.Length Then Return vLineIndex
                
                ' Check member type and parse accordingly
                Select Case lWords(lIndex).ToUpper()
                    Case "SUB"
                        Return ParseSubMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "FUNCTION"
                        Return ParseFunctionMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "PROPERTY"
                        Return ParsePropertyMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "EVENT"
                        Return ParseEventMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "CONST"
                        Return ParseConstMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "DELEGATE"
                        Return ParseDelegateMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case "OPERATOR"
                        Return ParseOperatorMember(vParentNode, vLineIndex, lWords, lIndex, lModifiers)
                        
                    Case Else
                        ' Check if it's a field declaration or enum value
                        If vParentNode.NodeType = CodeNodeType.eEnum Then
                            Return ParseEnumValue(vParentNode, vLineIndex, lTrimmedLine)
                        ElseIf lTrimmedLine.Contains(" As ") OrElse lTrimmedLine.Contains("=") Then
                            Return ParseFieldMember(vParentNode, vLineIndex, lTrimmedLine, lModifiers)
                        End If
                End Select
                
                Return vLineIndex
                
            Catch ex As Exception
                Console.WriteLine($"ParseMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseSubMember(vParentNode As SyntaxNode, vLineIndex As Integer, vWords() As String, vStartIndex As Integer, vModifiers As List(Of String)) As Integer
            Try
                Dim lIndex As Integer = vStartIndex + 1 ' Skip "SUB"
                If lIndex >= vWords.Length Then Return vLineIndex
                
                Dim lName As String = vWords(lIndex).Replace("(", "")
                
                ' Check if it's a constructor
                Dim lNodeType As CodeNodeType = If(lName.ToUpper() = "NEW", CodeNodeType.eConstructor, CodeNodeType.eMethod)
                
                Dim lMethodNode As New SyntaxNode(lNodeType, lName)
                lMethodNode.StartLine = vLineIndex
                
                ' Find the end of the method
                lMethodNode.EndLine = FindMethodEnd(vLineIndex, "SUB")
                
                StoreFileInfo(lMethodNode)
                SetModifiers(lMethodNode, String.Join(" ", vModifiers))
                
                ' Parse parameters if present
                ParseMethodParameters(lMethodNode, pLines(vLineIndex))
                
                vParentNode.AddChild(lMethodNode)
                'Console.WriteLine($"    Added {If(lNodeType = CodeNodeType.eConstructor, "constructor", "method")}: {lName}")
                
                Return lMethodNode.EndLine
                
            Catch ex As Exception
                Console.WriteLine($"ParseSubMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseFunctionMember(vParentNode As SyntaxNode, vLineIndex As Integer, vWords() As String, vStartIndex As Integer, vModifiers As List(Of String)) As Integer
            Try
                Dim lIndex As Integer = vStartIndex + 1 ' Skip "FUNCTION"
                If lIndex >= vWords.Length Then Return vLineIndex
                
                Dim lName As String = vWords(lIndex).Replace("(", "")
                
                Dim lFunctionNode As New SyntaxNode(CodeNodeType.eFunction, lName)
                lFunctionNode.StartLine = vLineIndex
                lFunctionNode.EndLine = FindMethodEnd(vLineIndex, "FUNCTION")
                StoreFileInfo(lFunctionNode)
                SetModifiers(lFunctionNode, String.Join(" ", vModifiers))
                
                ' Parse parameters and return type
                ParseMethodParameters(lFunctionNode, pLines(vLineIndex))
                ParseReturnType(lFunctionNode, pLines(vLineIndex))
                
                vParentNode.AddChild(lFunctionNode)
                'Console.WriteLine($"    Added function: {lName}")
                
                Return lFunctionNode.EndLine
                
            Catch ex As Exception
                Console.WriteLine($"ParseFunctionMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParsePropertyMember(vParentNode As SyntaxNode, vLineIndex As Integer, vWords() As String, vStartIndex As Integer, vModifiers As List(Of String)) As Integer
            Try
                Dim lIndex As Integer = vStartIndex + 1 ' Skip "PROPERTY"
                If lIndex >= vWords.Length Then Return vLineIndex
                
                Dim lName As String = vWords(lIndex).Replace("(", "")
                
                Dim lPropertyNode As New SyntaxNode(CodeNodeType.eProperty, lName)
                lPropertyNode.StartLine = vLineIndex
                lPropertyNode.EndLine = FindPropertyEnd(vLineIndex)
                StoreFileInfo(lPropertyNode)
                SetModifiers(lPropertyNode, String.Join(" ", vModifiers))
                
                ' Parse property type
                ParseReturnType(lPropertyNode, pLines(vLineIndex))
                
                vParentNode.AddChild(lPropertyNode)
                'Console.WriteLine($"    Added property: {lName}")
                
                Return lPropertyNode.EndLine
                
            Catch ex As Exception
                Console.WriteLine($"ParsePropertyMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseEventMember(vParentNode As SyntaxNode, vLineIndex As Integer, vWords() As String, vStartIndex As Integer, vModifiers As List(Of String)) As Integer
            Try
                Dim lIndex As Integer = vStartIndex + 1 ' Skip "EVENT"
                If lIndex >= vWords.Length Then Return vLineIndex
                
                Dim lName As String = vWords(lIndex).Replace("(", "")
                
                Dim lEventNode As New SyntaxNode(CodeNodeType.eEvent, lName)
                lEventNode.StartLine = vLineIndex
                lEventNode.EndLine = vLineIndex
                StoreFileInfo(lEventNode)
                SetModifiers(lEventNode, String.Join(" ", vModifiers))
                
                ' Parse event parameters
                ParseMethodParameters(lEventNode, pLines(vLineIndex))
                
                vParentNode.AddChild(lEventNode)
                'Console.WriteLine($"    Added event: {lName}")
                
                Return vLineIndex
                
            Catch ex As Exception
                Console.WriteLine($"ParseEventMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseConstMember(vParentNode As SyntaxNode, vLineIndex As Integer, vWords() As String, vStartIndex As Integer, vModifiers As List(Of String)) As Integer
            Try
                Dim lIndex As Integer = vStartIndex + 1 ' Skip "CONST"
                If lIndex >= vWords.Length Then Return vLineIndex
                
                Dim lName As String = vWords(lIndex)
                
                Dim lConstNode As New SyntaxNode(CodeNodeType.eConst, lName)
                lConstNode.StartLine = vLineIndex
                lConstNode.EndLine = vLineIndex
                StoreFileInfo(lConstNode)
                SetModifiers(lConstNode, String.Join(" ", vModifiers))
                
                vParentNode.AddChild(lConstNode)
                'Console.WriteLine($"    Added const: {lName}")
                
                Return vLineIndex
                
            Catch ex As Exception
                Console.WriteLine($"ParseConstMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseDelegateMember(vParentNode As SyntaxNode, vLineIndex As Integer, vWords() As String, vStartIndex As Integer, vModifiers As List(Of String)) As Integer
            Try
                Dim lIndex As Integer = vStartIndex + 1 ' Skip "DELEGATE"
                
                ' Skip "Sub" or "Function" if present
                If lIndex < vWords.Length AndAlso (vWords(lIndex).ToUpper() = "SUB" OrElse vWords(lIndex).ToUpper() = "FUNCTION") Then
                    lIndex += 1
                End If
                
                If lIndex >= vWords.Length Then Return vLineIndex
                
                Dim lName As String = vWords(lIndex).Replace("(", "")
                
                Dim lDelegateNode As New SyntaxNode(CodeNodeType.eDelegate, lName)
                lDelegateNode.StartLine = vLineIndex
                lDelegateNode.EndLine = vLineIndex
                StoreFileInfo(lDelegateNode)
                SetModifiers(lDelegateNode, String.Join(" ", vModifiers))
                
                vParentNode.AddChild(lDelegateNode)
                'Console.WriteLine($"    Added delegate: {lName}")
                
                Return vLineIndex
                
            Catch ex As Exception
                Console.WriteLine($"ParseDelegateMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseOperatorMember(vParentNode As SyntaxNode, vLineIndex As Integer, vWords() As String, vStartIndex As Integer, vModifiers As List(Of String)) As Integer
            Try
                Dim lIndex As Integer = vStartIndex + 1 ' Skip "OPERATOR"
                If lIndex >= vWords.Length Then Return vLineIndex
                
                Dim lName As String = "Operator " & vWords(lIndex)
                
                Dim lOperatorNode As New SyntaxNode(CodeNodeType.eOperator, lName)
                lOperatorNode.StartLine = vLineIndex
                lOperatorNode.EndLine = FindMethodEnd(vLineIndex, "OPERATOR")
                StoreFileInfo(lOperatorNode)
                SetModifiers(lOperatorNode, String.Join(" ", vModifiers))
                
                vParentNode.AddChild(lOperatorNode)
                'Console.WriteLine($"    Added operator: {lName}")
                
                Return lOperatorNode.EndLine
                
            Catch ex As Exception
                Console.WriteLine($"ParseOperatorMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseFieldMember(vParentNode As SyntaxNode, vLineIndex As Integer, vLine As String, vModifiers As List(Of String)) As Integer
            Try
                ' Extract field name from declaration
                Dim lName As String = ExtractFieldName(vLine)
                If String.IsNullOrEmpty(lName) Then Return vLineIndex
                
                Dim lFieldNode As New SyntaxNode(CodeNodeType.eField, lName)
                lFieldNode.StartLine = vLineIndex
                lFieldNode.EndLine = vLineIndex
                StoreFileInfo(lFieldNode)
                SetModifiers(lFieldNode, String.Join(" ", vModifiers))
                
                ' Parse field type if present
                ParseFieldType(lFieldNode, vLine)
                
                vParentNode.AddChild(lFieldNode)
                'Console.WriteLine($"    Added field: {lName}")
                
                Return vLineIndex
                
            Catch ex As Exception
                Console.WriteLine($"ParseFieldMember error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        Private Function ParseEnumValue(vParentNode As SyntaxNode, vLineIndex As Integer, vLine As String) As Integer
            Try
                ' Extract enum value name
                Dim lName As String = vLine.Trim()
                Dim lEqualsIndex As Integer = lName.IndexOf("="c)
                
                If lEqualsIndex > 0 Then
                    lName = lName.Substring(0, lEqualsIndex).Trim()
                End If
                
                ' Remove trailing comma if present
                If lName.EndsWith(",") Then
                    lName = lName.Substring(0, lName.Length - 1).Trim()
                End If
                
                If String.IsNullOrEmpty(lName) Then Return vLineIndex
                
                Dim lEnumValueNode As New SyntaxNode(CodeNodeType.eEnumValue, lName)
                lEnumValueNode.StartLine = vLineIndex
                lEnumValueNode.EndLine = vLineIndex
                StoreFileInfo(lEnumValueNode)
                
                vParentNode.AddChild(lEnumValueNode)
                Console.WriteLine($"    Added enum value: {lName}")
                
                Return vLineIndex
                
            Catch ex As Exception
                Console.WriteLine($"ParseEnumValue error: {ex.Message}")
                Return vLineIndex
            End Try
        End Function
        
        ' ===== Process Top-Level Members =====
        
        Private Sub ProcessTopLevelMembers(vRootNamespaceNode As SyntaxNode)
            Try
                ' Process any members that aren't in blocks (like module-level fields)
                ' This is rare in modern VB.NET but can happen
                
                Dim lInBlock As Boolean = False
                Dim lBlockRanges As New List(Of Tuple(Of Integer, Integer))()
                
                ' Collect all block ranges
                Dim lBlocks As List(Of BlockInfo) = FindTopLevelBlocks()
                For Each lBlock In lBlocks
                    lBlockRanges.Add(New Tuple(Of Integer, Integer)(lBlock.StartLine, lBlock.EndLine))
                Next
                
                ' Check each line
                For i As Integer = 0 To pLines.Length - 1
                    ' Skip if in a block
                    lInBlock = False
                    For Each lRange In lBlockRanges
                        If i >= lRange.Item1 AndAlso i <= lRange.Item2 Then
                            lInBlock = True
                            Exit For
                        End If
                    Next
                    
                    If Not lInBlock Then
                        ' Parse as top-level member
                        Dim lEndLine As Integer = ParseMember(vRootNamespaceNode, i)
                        If lEndLine > i Then
                            i = lEndLine ' Skip to end of member
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"ProcessTopLevelMembers error: {ex.Message}")
            End Try
        End Sub
        
' Syntax/VBParser.Unified.Part3.vb - Final part of unified recursive parser
' This is the third and final part of VBParser.Unified.vb

        ' ===== Helper Methods (continued from Part 2) =====
        
        Private Function FindMethodEnd(vStartLine As Integer, vMethodType As String) As Integer
            Try
                Dim lEndKeyword As String = "END " & vMethodType
                
                For i As Integer = vStartLine + 1 To pLines.Length - 1
                    Dim lLine As String = pLines(i).Trim().ToUpper()
                    
                    If lLine.StartsWith(lEndKeyword) Then
                        Return i
                    End If
                Next
                
                ' If no end found, check if it's a single-line method
                Dim lStartLineText As String = pLines(vStartLine).Trim()
                If Not lStartLineText.EndsWith("_") Then
                    ' Likely a single-line declaration or abstract method
                    Return vStartLine
                End If
                
                ' Otherwise assume end of file
                Console.WriteLine($"Warning: No END {vMethodType} found for method at line {vStartLine}")
                Return pLines.Length - 1
                
            Catch ex As Exception
                Console.WriteLine($"FindMethodEnd error: {ex.Message}")
                Return vStartLine
            End Try
        End Function
        
        Private Function FindChildByNameAndType(vParentNode As SyntaxNode, vName As String, vNodeType As CodeNodeType) As SyntaxNode
            Try
                If vParentNode Is Nothing OrElse String.IsNullOrEmpty(vName) Then Return Nothing
                
                For Each lChild In vParentNode.Children
                    If String.Equals(lChild.Name, vName, StringComparison.OrdinalIgnoreCase) AndAlso 
                       lChild.NodeType = vNodeType Then
                        Return lChild
                    End If
                Next
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"FindChildByNameAndType error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Sub StoreFileInfo(vNode As SyntaxNode)
            Try
                If vNode.Attributes Is Nothing Then
                    vNode.Attributes = New Dictionary(Of String, String)()
                End If
                
                vNode.Attributes("FilePath") = pCurrentFilePath
                vNode.Attributes("FileName") = pCurrentFileName
                
            Catch ex As Exception
                Console.WriteLine($"StoreFileInfo error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub SetModifiers(vNode As SyntaxNode, vModifiers As String)
            Try
                If String.IsNullOrEmpty(vModifiers) Then
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic ' Default for most types
                    Return
                End If
                
                Dim lModUpper As String = vModifiers.ToUpper()
                
                ' Set visibility
                If lModUpper.Contains("PUBLIC") Then
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic
                ElseIf lModUpper.Contains("PRIVATE") Then
                    vNode.Visibility = SyntaxNode.eVisibility.ePrivate
                ElseIf lModUpper.Contains("PROTECTED") Then
                    If lModUpper.Contains("FRIEND") Then
                        vNode.Visibility = SyntaxNode.eVisibility.eProtectedFriend
                    Else
                        vNode.Visibility = SyntaxNode.eVisibility.eProtected
                    End If
                ElseIf lModUpper.Contains("FRIEND") Then
                    vNode.Visibility = SyntaxNode.eVisibility.eFriend
                Else
                    ' Default visibility based on context
                    vNode.Visibility = SyntaxNode.eVisibility.ePublic
                End If
                
                ' Set other modifiers
                If lModUpper.Contains("PARTIAL") Then vNode.IsPartial = True
                If lModUpper.Contains("SHARED") OrElse lModUpper.Contains("STATIC") Then vNode.IsShared = True
                If lModUpper.Contains("OVERRIDABLE") Then vNode.IsOverridable = True
                If lModUpper.Contains("OVERRIDES") Then vNode.IsOverrides = True
                If lModUpper.Contains("MUSTINHERIT") Then vNode.IsAbstract = True
                If lModUpper.Contains("NOTINHERITABLE") Then vNode.IsSealed = True
                If lModUpper.Contains("MUSTOVERRIDE") Then vNode.IsAbstract = True
                If lModUpper.Contains("READONLY") Then vNode.IsReadOnly = True
                If lModUpper.Contains("WRITEONLY") Then vNode.IsWriteOnly = True
                If lModUpper.Contains("ASYNC") Then vNode.IsAsync = True
                If lModUpper.Contains("ITERATOR") Then vNode.IsIterator = True
                If lModUpper.Contains("SHADOWS") Then vNode.IsShadows = True
                
            Catch ex As Exception
                Console.WriteLine($"SetModifiers error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ParseInheritanceAndImplements(vNode As SyntaxNode, vFullLine As String)
            Try
                ' Check for Inherits
                Dim lInheritsIndex As Integer = vFullLine.IndexOf(" Inherits ", StringComparison.OrdinalIgnoreCase)
                If lInheritsIndex > 0 Then
                    Dim lInheritsStart As Integer = lInheritsIndex + " Inherits ".Length
                    Dim lNextKeywordIndex As Integer = vFullLine.IndexOf(" Implements ", lInheritsStart, StringComparison.OrdinalIgnoreCase)
                    
                    Dim lInheritsText As String
                    If lNextKeywordIndex > 0 Then
                        lInheritsText = vFullLine.Substring(lInheritsStart, lNextKeywordIndex - lInheritsStart).Trim()
                    Else
                        lInheritsText = vFullLine.Substring(lInheritsStart).Trim()
                    End If
                    
                    vNode.BaseType = lInheritsText
                    vNode.InheritsList.Add(lInheritsText)
                End If
                
                ' Check for Implements
                Dim lImplementsIndex As Integer = vFullLine.IndexOf(" Implements ", StringComparison.OrdinalIgnoreCase)
                If lImplementsIndex > 0 Then
                    Dim lImplementsStart As Integer = lImplementsIndex + " Implements ".Length
                    Dim lImplementsText As String = vFullLine.Substring(lImplementsStart).Trim()
                    
                    ' Split multiple interfaces
                    Dim lInterfaces As String() = lImplementsText.Split(","c)
                    For Each lInterface In lInterfaces
                        Dim lTrimmedInterface As String = lInterface.Trim()
                        If Not String.IsNullOrEmpty(lTrimmedInterface) Then
                            vNode.ImplementsList.Add(lTrimmedInterface)
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseInheritanceAndImplements error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ParseMethodParameters(vNode As SyntaxNode, vLine As String)
            Try
                ' Extract parameters from between parentheses
                Dim lOpenParen As Integer = vLine.IndexOf("("c)
                Dim lCloseParen As Integer = vLine.LastIndexOf(")"c)
                
                If lOpenParen > 0 AndAlso lCloseParen > lOpenParen Then
                    Dim lParamsText As String = vLine.Substring(lOpenParen + 1, lCloseParen - lOpenParen - 1).Trim()
                    
                    If Not String.IsNullOrEmpty(lParamsText) Then
                        ' Split parameters by comma (simple parsing - doesn't handle nested generics perfectly)
                        Dim lParams As String() = lParamsText.Split(","c)
                        
                        For Each lParam In lParams
                            Dim lTrimmedParam As String = lParam.Trim()
                            If Not String.IsNullOrEmpty(lTrimmedParam) Then
                                Dim lParamNode As New SyntaxNode(CodeNodeType.eParameter, ExtractParameterName(lTrimmedParam))
                                lParamNode.StartLine = vNode.StartLine
                                lParamNode.EndLine = vNode.StartLine
                                
                                ' Store full parameter declaration
                                If lParamNode.Attributes Is Nothing Then
                                    lParamNode.Attributes = New Dictionary(Of String, String)()
                                End If
                                lParamNode.Attributes("Declaration") = lTrimmedParam
                                
                                vNode.AddChild(lParamNode)
                            End If
                        Next
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseMethodParameters error: {ex.Message}")
            End Try
        End Sub
        
        Private Function ExtractParameterName(vParamDeclaration As String) As String
            Try
                ' Remove ByVal, ByRef, Optional, ParamArray
                Dim lDecl As String = vParamDeclaration.Trim()
                
                For Each lKeyword In {"ByVal ", "ByRef ", "Optional ", "ParamArray "}
                    If lDecl.StartsWith(lKeyword, StringComparison.OrdinalIgnoreCase) Then
                        lDecl = lDecl.Substring(lKeyword.Length).Trim()
                    End If
                Next
                
                ' Extract name (before As or =)
                Dim lAsIndex As Integer = lDecl.IndexOf(" As ", StringComparison.OrdinalIgnoreCase)
                Dim lEqualsIndex As Integer = lDecl.IndexOf("="c)
                
                Dim lEndIndex As Integer = lDecl.Length
                If lAsIndex > 0 AndAlso lAsIndex < lEndIndex Then lEndIndex = lAsIndex
                If lEqualsIndex > 0 AndAlso lEqualsIndex < lEndIndex Then lEndIndex = lEqualsIndex
                
                If lEndIndex > 0 Then
                    Return lDecl.Substring(0, lEndIndex).Trim()
                End If
                
                Return lDecl
                
            Catch ex As Exception
                Console.WriteLine($"ExtractParameterName error: {ex.Message}")
                Return "param"
            End Try
        End Function
        
        Private Sub ParseReturnType(vNode As SyntaxNode, vLine As String)
            Try
                ' Extract return type after "As"
                Dim lAsIndex As Integer = vLine.LastIndexOf(" As ", StringComparison.OrdinalIgnoreCase)
                
                If lAsIndex > 0 Then
                    Dim lTypeStart As Integer = lAsIndex + 4
                    Dim lReturnType As String = vLine.Substring(lTypeStart).Trim()
                    
                    ' Remove any trailing characters
                    Dim lInvalidChars As Char() = {"("c, "="c, "'"c}
                    For Each lChar In lInvalidChars
                        Dim lCharIndex As Integer = lReturnType.IndexOf(lChar)
                        If lCharIndex > 0 Then
                            lReturnType = lReturnType.Substring(0, lCharIndex).Trim()
                        End If
                    Next
                    
                    If Not String.IsNullOrEmpty(lReturnType) Then
                        vNode.ReturnType = lReturnType
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseReturnType error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ParseFieldType(vNode As SyntaxNode, vLine As String)
            Try
                ' Extract field type after "As"
                Dim lAsIndex As Integer = vLine.IndexOf(" As ", StringComparison.OrdinalIgnoreCase)
                
                If lAsIndex > 0 Then
                    Dim lTypeStart As Integer = lAsIndex + 4
                    Dim lFieldType As String = vLine.Substring(lTypeStart).Trim()
                    
                    ' Remove initialization if present
                    Dim lEqualsIndex As Integer = lFieldType.IndexOf("="c)
                    If lEqualsIndex > 0 Then
                        lFieldType = lFieldType.Substring(0, lEqualsIndex).Trim()
                    End If
                    
                    If Not String.IsNullOrEmpty(lFieldType) Then
                        vNode.ReturnType = lFieldType ' Using ReturnType to store field type
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseFieldType error: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetNodeSummary(vNode As SyntaxNode) As String
            Try
                Dim lCounts As New Dictionary(Of CodeNodeType, Integer)()
                CountNodeTypes(vNode, lCounts)
                
                Dim lSummary As New List(Of String)()
                For Each lPair In lCounts.OrderBy(Function(p) p.Key.ToString())
                    If lPair.Value > 0 Then
                        lSummary.Add($"{lPair.Value} {lPair.Key}")
                    End If
                Next
                
                If lSummary.Count > 0 Then
                    Return String.Join(", ", lSummary)
                Else
                    Return "empty structure"
                End If
                
            Catch ex As Exception
                Return "unknown structure"
            End Try
        End Function
        
        Private Sub CountNodeTypes(vNode As SyntaxNode, vCounts As Dictionary(Of CodeNodeType, Integer))
            Try
                If vNode Is Nothing Then Return
                
                If Not vCounts.ContainsKey(vNode.NodeType) Then
                    vCounts(vNode.NodeType) = 0
                End If
                vCounts(vNode.NodeType) += 1
                
                For Each lChild In vNode.Children
                    CountNodeTypes(lChild, vCounts)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"CountNodeTypes error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Utility Methods =====
        
        ''' <summary>
        ''' Print the structure tree for debugging
        ''' </summary>
        Public Sub PrintStructure(vNode As SyntaxNode, Optional vIndent As Integer = 0)
            Try
                If vNode Is Nothing Then Return
                
                Dim lIndentStr As String = New String(" "c, vIndent * 2)
                Dim lInfo As String = $"{vNode.Name} ({vNode.NodeType})"
                
                ' Add modifiers info
                If vNode.IsPartial Then lInfo &= " [PARTIAL]"
                If vNode.IsShared Then lInfo &= " [SHARED]"
                If vNode.IsAbstract Then lInfo &= " [ABSTRACT]"
                If vNode.Visibility <> SyntaxNode.eVisibility.ePublic Then
                    lInfo &= $" [{vNode.Visibility}]"
                End If
                
                ' Add line info
                If vNode.StartLine >= 0 Then 
                    lInfo &= $" L{vNode.StartLine}"
                    If vNode.EndLine <> vNode.StartLine Then
                        lInfo &= $"-{vNode.EndLine}"
                    End If
                End If
                
                ' Add type info
                If Not String.IsNullOrEmpty(vNode.ReturnType) Then
                    lInfo &= $" : {vNode.ReturnType}"
                End If
                
                Console.WriteLine($"{lIndentStr}{lInfo}")
                
                ' Print children
                For Each lChild In vNode.Children
                    PrintStructure(lChild, vIndent + 1)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"PrintStructure error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Get the project root namespace (for cross-file operations)
        ''' </summary>
        Public Function GetProjectRootNamespace() As SyntaxNode
            Return pProjectRootNamespace
        End Function
        
        ''' <summary>
        ''' Set the project root namespace (for cross-file operations)
        ''' </summary>
        Public Sub SetProjectRootNamespace(vRootNamespace As SyntaxNode)
            pProjectRootNamespace = vRootNamespace
        End Sub
        
    

        
        Private Function FindPropertyEnd(vStartLine As Integer) As Integer
            Try
                ' Properties can be auto-implemented (single line) or have Get/Set blocks
                Dim lStartLineText As String = pLines(vStartLine).Trim()
                
                ' Check for auto-implemented property (no Get/Set on same line)
                If Not lStartLineText.ToUpper().Contains(" GET") AndAlso 
                   Not lStartLineText.ToUpper().Contains(" SET") Then
                    ' Check next line for Get or Set
                    If vStartLine + 1 < pLines.Length Then
                        Dim lNextLine As String = pLines(vStartLine + 1).Trim().ToUpper()
                        If Not lNextLine.StartsWith("GET") AndAlso Not lNextLine.StartsWith("SET") Then
                            ' Auto-implemented property
                            Return vStartLine
                        End If
                    Else
                        ' Last line of file
                        Return vStartLine
                    End If
                End If
                
                ' Look for End Property
                For i As Integer = vStartLine + 1 To pLines.Length - 1
                    Dim lLine As String = pLines(i).Trim().ToUpper()
                    
                    If lLine.StartsWith("END PROPERTY") Then
                        Return i
                    End If
                Next
                
                ' If no end found, assume single line
                Return vStartLine
                
            Catch ex As Exception
                Console.WriteLine($"FindPropertyEnd error: {ex.Message}")
                Return vStartLine
            End Try
        End Function

        ''' <summary>
        ''' Enhanced FindBlockEnd that properly handles End Namespace statements
        ''' </summary>
        Private Function FindBlockEnd(vStartLine As Integer, vBlockType As String) As Integer
            Try
                ' Build the END statement we're looking for
                Dim lEndKeyword As String = "END " & vBlockType.ToUpper()
                
                ' Special handling for namespaces - also look for "End Namespace" (two words)
                Dim lAlternateEndKeyword As String = ""
                If vBlockType.ToUpper() = "NAMESPACE" Then
                    lAlternateEndKeyword = "END NAMESPACE"
                End If
                
                ' Track nesting level for nested blocks
                Dim lNestLevel As Integer = 1
                
                ' Look for the matching END statement
                For i As Integer = vStartLine + 1 To pLines.Length - 1
                    Dim lLine As String = pLines(i).Trim().ToUpper()
                    
                    ' Skip empty lines and comments
                    If String.IsNullOrWhiteSpace(lLine) OrElse lLine.StartsWith("'") Then
                        Continue For
                    End If
                    
                    ' Check for nested block of same type
                    If lLine.StartsWith(vBlockType.ToUpper() & " ") OrElse
                       (vBlockType = "SUB" AndAlso lLine.StartsWith("FUNCTION ")) OrElse
                       (vBlockType = "FUNCTION" AndAlso lLine.StartsWith("SUB ")) Then
                        lNestLevel += 1
                    End If
                    
                    ' Check for END statement - handle both formats
                    If lLine = lEndKeyword OrElse 
                       lLine.StartsWith(lEndKeyword & " ") OrElse
                       (Not String.IsNullOrEmpty(lAlternateEndKeyword) AndAlso 
                        (lLine = lAlternateEndKeyword OrElse lLine.StartsWith(lAlternateEndKeyword & " "))) Then
                        
                        lNestLevel -= 1
                        If lNestLevel = 0 Then
                            Console.WriteLine($"  Found {lEndKeyword} at line {i}")
                            Return i
                        End If
                    End If
                Next
                
                ' If no end found, check if this might be the last block in file
                ' For namespaces, this is common in VB.NET when the namespace extends to end of file
                If vBlockType.ToUpper() = "NAMESPACE" Then
                    Console.WriteLine($"  Namespace extends to end of file (line {pLines.Length - 1})")
                    Return pLines.Length - 1
                Else
                    Console.WriteLine($"Warning: No END {vBlockType} found for block starting at line {vStartLine}")
                    Return pLines.Length - 1
                End If
                
            Catch ex As Exception
                Console.WriteLine($"FindBlockEndFixed error: {ex.Message}")
                Return pLines.Length - 1
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced ProcessNamespaceBlock that handles sub-namespaces properly
        ''' </summary>
        Private Function ProcessNamespaceBlock(vBlock As BlockInfo, vParentNode As SyntaxNode) As SyntaxNode
            Try
                ' For sub-namespaces (like "Syntax" in a file that's in the SimpleIDE namespace),
                ' we need to check if this should be a child of the root namespace
                
                Console.WriteLine($"  Processing namespace block: {vBlock.Name}")
                Console.WriteLine($"    Parent node: {vParentNode.Name} (Type: {vParentNode.NodeType})")
                
                ' Check if namespace already exists (from another file or partial definition)
                Dim lExistingNamespace As SyntaxNode = FindChildByNameAndType(vParentNode, vBlock.Name, CodeNodeType.eNamespace)
                
                If lExistingNamespace IsNot Nothing Then
                    Console.WriteLine($"    Found existing namespace: {vBlock.Name}")
                    Return lExistingNamespace
                End If
                
                ' Create new namespace
                Dim lNamespaceNode As New SyntaxNode(CodeNodeType.eNamespace, vBlock.Name)
                lNamespaceNode.StartLine = vBlock.StartLine
                lNamespaceNode.EndLine = vBlock.EndLine
                lNamespaceNode.IsImplicit = False ' This is an explicit namespace declaration
                StoreFileInfo(lNamespaceNode)
                
                vParentNode.AddChild(lNamespaceNode)
                Console.WriteLine($"    Created namespace: {vBlock.Name} as child of {vParentNode.Name}")
                
                Return lNamespaceNode
                
            Catch ex As Exception
                Console.WriteLine($"ProcessNamespaceBlockEnhanced error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced DetectBlockStart that better handles namespace declarations
        ''' </summary>
        Private Function DetectBlockStart(vLineIndex As Integer) As BlockInfo
            Dim lBlock As New BlockInfo()
            lBlock.StartLine = vLineIndex
            
            Try
                Dim lLine As String = pLines(vLineIndex)
                Dim lTrimmedLine As String = lLine.Trim()
                lBlock.FullLine = lLine
                
                ' Remove any trailing comments
                Dim lCommentIndex As Integer = lTrimmedLine.IndexOf("'"c)
                If lCommentIndex >= 0 Then
                    lTrimmedLine = lTrimmedLine.Substring(0, lCommentIndex).Trim()
                End If
                
                ' Split into words for easier parsing
                Dim lWords As String() = lTrimmedLine.Split({" "c, vbTab}, StringSplitOptions.RemoveEmptyEntries)
                If lWords.Length = 0 Then Return lBlock
                
                Dim lIndex As Integer = 0
                Dim lFirstWord As String = lWords(lIndex).ToUpper()
                
                ' Check for Namespace (no modifiers allowed before namespace)
                If lFirstWord = "NAMESPACE" Then
                    lBlock.BlockType = "NAMESPACE"
                    lIndex += 1
                    If lIndex < lWords.Length Then
                        ' Collect the full namespace name (might be dotted like "SimpleIDE.Syntax")
                        Dim lNameParts As New List(Of String)()
                        While lIndex < lWords.Length
                            Dim lWord As String = lWords(lIndex)
                            ' Stop at any keyword or comment
                            If lWord.StartsWith("'") Then Exit While
                            lNameParts.Add(lWord)
                            lIndex += 1
                        End While
                        lBlock.Name = String.Join(" ", lNameParts).Trim()
                    End If
                    Return lBlock
                End If
                
                ' Collect modifiers
                Dim lModifiers As New List(Of String)()
                
                ' Keep collecting modifiers until we hit a declaration keyword
                While lIndex < lWords.Length
                    Dim lWord As String = lWords(lIndex).ToUpper()
                    
                    Select Case lWord
                        Case "PUBLIC", "PRIVATE", "PROTECTED", "FRIEND", "PARTIAL",
                             "SHARED", "STATIC", "NOTINHERITABLE", "MUSTINHERIT",
                             "NOTOVERRIDABLE", "OVERRIDABLE", "MUSTOVERRIDE",
                             "OVERRIDES", "OVERLOADS", "SHADOWS", "DEFAULT"
                            lModifiers.Add(lWords(lIndex))
                            lIndex += 1
                            
                        Case "CLASS", "MODULE", "INTERFACE", "STRUCTURE", "ENUM"
                            lBlock.BlockType = lWord
                            lBlock.Modifiers = String.Join(" ", lModifiers)
                            lIndex += 1
                            If lIndex < lWords.Length Then
                                lBlock.Name = lWords(lIndex)
                            End If
                            Return lBlock
                            
                        Case Else
                            ' Not a block start
                            Return lBlock
                    End Select
                End While
                
            Catch ex As Exception
                Console.WriteLine($"DetectBlockStartEnhanced error: {ex.Message}")
            End Try
            
            Return lBlock
        End Function
        
        Private Sub ProcessBlock(vBlock As BlockInfo, vParentNode As SyntaxNode)
            Try
                Console.WriteLine($"Processing {vBlock.BlockType} '{vBlock.Name}' (lines {vBlock.StartLine}-{vBlock.EndLine})")
                
                ' Use enhanced FindBlockEnd
                vBlock.EndLine = FindBlockEnd(vBlock.StartLine, vBlock.BlockType)
                
                ' Create or find the node for this block
                Dim lNode As SyntaxNode = Nothing
                
                Select Case vBlock.BlockType
                    Case "NAMESPACE"
                        lNode = ProcessNamespaceBlock(vBlock, vParentNode)
                        
                    Case "CLASS"
                        lNode = ProcessClassBlock(vBlock, vParentNode)
                        
                    Case "MODULE"
                        lNode = ProcessModuleBlock(vBlock, vParentNode)
                        
                    Case "INTERFACE"
                        lNode = ProcessInterfaceBlock(vBlock, vParentNode)
                        
                    Case "STRUCTURE"
                        lNode = ProcessStructureBlock(vBlock, vParentNode)
                        
                    Case "ENUM"
                        lNode = ProcessEnumBlock(vBlock, vParentNode)
                End Select
                
                ' If we created a node, parse its contents
                If lNode IsNot Nothing Then
                    ParseNodeContents(lNode, vBlock.StartLine + 1, vBlock.EndLine - 1)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ProcessBlockEnhanced error: {ex.Message}")
            End Try
        End Sub

    End Class


End Namespace